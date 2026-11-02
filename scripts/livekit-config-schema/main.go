package main

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"flag"
	"fmt"
	"go/types"
	"os"
	"path/filepath"
	"reflect"
	"strings"

	"golang.org/x/tools/go/packages"
)

type generator struct {
	definitions map[string]any
}

func main() {
	dir := flag.String("dir", ".", "path to the upstream Go module")
	pattern := flag.String("package", "./pkg/config", "go package pattern containing the root configuration type")
	typeName := flag.String("type", "Config", "root configuration type")
	output := flag.String("out", "-", "output file, or - for stdout")
	schemaInput := flag.String("from-schema", "", "generate C# models from an existing JSON Schema file")
	csharpOutput := flag.String("csharp-out", "", "optional C# model output file")
	csharpNamespace := flag.String("csharp-namespace", "", "namespace for generated C# models")
	csharpRootType := flag.String("csharp-root-type", "", "root C# model name")
	flag.Parse()

	if *schemaInput != "" {
		if *csharpOutput == "" || *csharpNamespace == "" || *csharpRootType == "" {
			fmt.Fprintln(os.Stderr, "-csharp-out, -csharp-namespace, and -csharp-root-type are required with -from-schema")
			os.Exit(1)
		}
		data, err := os.ReadFile(*schemaInput)
		if err != nil {
			fmt.Fprintf(os.Stderr, "read schema: %v\n", err)
			os.Exit(1)
		}
		schema, err := decodeSchema(data)
		if err != nil {
			fmt.Fprintf(os.Stderr, "parse schema: %v\n", err)
			os.Exit(1)
		}
		models, err := generateCSharp(schema, *csharpNamespace, *csharpRootType)
		if err != nil {
			fmt.Fprintf(os.Stderr, "generate C# models: %v\n", err)
			os.Exit(1)
		}
		if err := os.WriteFile(*csharpOutput, models, 0o644); err != nil {
			fmt.Fprintf(os.Stderr, "write C# models: %v\n", err)
			os.Exit(1)
		}
		return
	}

	schema, err := generate(*dir, *pattern, *typeName)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}

	data, err := json.MarshalIndent(schema, "", "  ")
	if err != nil {
		fmt.Fprintf(os.Stderr, "encode schema: %v\n", err)
		os.Exit(1)
	}
	data = append(data, '\n')

	if *output == "-" {
		_, err = os.Stdout.Write(data)
	} else {
		err = os.WriteFile(*output, data, 0o644)
	}
	if err != nil {
		fmt.Fprintf(os.Stderr, "write schema: %v\n", err)
		os.Exit(1)
	}

}

func generate(dir, pattern, rootType string) (map[string]any, error) {
	absoluteDir, err := filepath.Abs(dir)
	if err != nil {
		return nil, fmt.Errorf("resolve module directory: %w", err)
	}

	loaded, err := packages.Load(&packages.Config{
		Dir:  absoluteDir,
		Mode: packages.LoadAllSyntax,
	}, pattern)
	if err != nil {
		return nil, fmt.Errorf("load Go package %q: %w", pattern, err)
	}
	if len(loaded) != 1 {
		return nil, fmt.Errorf("expected one package for %q, got %d", pattern, len(loaded))
	}
	if packages.PrintErrors(loaded) > 0 {
		fmt.Fprintf(
			os.Stderr,
			"warning: Go package %q has load or type-check errors; referenced unresolved types will fail schema generation\n",
			pattern,
		)
	}

	rootObject := loaded[0].Types.Scope().Lookup(rootType)
	if rootObject == nil {
		return nil, fmt.Errorf("type %q was not found in package %q", rootType, loaded[0].PkgPath)
	}
	root, ok := rootObject.(*types.TypeName)
	if !ok {
		return nil, fmt.Errorf("%q in package %q is not a type", rootType, loaded[0].PkgPath)
	}

	g := &generator{definitions: make(map[string]any)}
	rootSchema, err := g.inlineSchema(root.Type())
	if err != nil {
		return nil, fmt.Errorf("generate schema for %s.%s: %w", loaded[0].PkgPath, rootType, err)
	}

	rootSchema["$schema"] = "https://json-schema.org/draft/2020-12/schema"
	rootSchema["title"] = rootType
	rootSchema["x-go-package"] = loaded[0].PkgPath
	if len(g.definitions) > 0 {
		rootSchema["$defs"] = g.definitions
	}
	return rootSchema, nil
}

func (g *generator) schema(typ types.Type) (map[string]any, error) {
	typ = types.Unalias(typ)
	switch typ := typ.(type) {
	case *types.Pointer:
		value, err := g.schema(typ.Elem())
		if err != nil {
			return nil, err
		}
		return map[string]any{"anyOf": []any{value, map[string]any{"type": "null"}}}, nil
	case *types.Named:
		if typ.Obj().Pkg() != nil && typ.Obj().Pkg().Path() == "time" {
			switch typ.Obj().Name() {
			case "Duration":
				return map[string]any{"type": "string", "format": "duration"}, nil
			case "Time":
				return map[string]any{"type": "string", "format": "date-time"}, nil
			}
		}
		key := definitionKey(typ)
		if _, exists := g.definitions[key]; !exists {
			// Reserve the key before traversing fields so recursive Go types become $refs.
			g.definitions[key] = map[string]any{}
			definition, err := g.inlineSchema(typ.Underlying())
			if err != nil {
				delete(g.definitions, key)
				return nil, err
			}
			g.definitions[key] = definition
		}
		return map[string]any{"$ref": "#/$defs/" + key}, nil
	default:
		return g.inlineSchema(typ)
	}
}

func (g *generator) inlineSchema(typ types.Type) (map[string]any, error) {
	typ = types.Unalias(typ)
	switch typ := typ.(type) {
	case *types.Pointer:
		schema, err := g.inlineSchema(typ.Elem())
		if err != nil {
			return nil, err
		}
		return map[string]any{
			"anyOf": []any{schema, map[string]any{"type": "null"}},
		}, nil
	case *types.Named:
		return g.inlineSchema(typ.Underlying())
	case *types.Basic:
		if typ.Kind() == types.Invalid {
			return nil, fmt.Errorf("Go type is unresolved")
		}
		switch {
		case typ.Info()&types.IsBoolean != 0:
			return map[string]any{"type": "boolean"}, nil
		case typ.Info()&types.IsInteger != 0:
			return map[string]any{"type": "integer"}, nil
		case typ.Info()&types.IsFloat != 0:
			return map[string]any{"type": "number"}, nil
		case typ.Info()&types.IsString != 0:
			return map[string]any{"type": "string"}, nil
		case typ.Info()&types.IsComplex != 0:
			return map[string]any{"type": "string", "format": "go-complex"}, nil
		default:
			return map[string]any{}, nil
		}
	case *types.Slice:
		if basic, ok := typ.Elem().Underlying().(*types.Basic); ok && basic.Kind() == types.Byte {
			return map[string]any{"type": "string", "contentEncoding": "base64"}, nil
		}
		item, err := g.schema(typ.Elem())
		if err != nil {
			return nil, err
		}
		return map[string]any{"type": "array", "items": item}, nil
	case *types.Array:
		item, err := g.schema(typ.Elem())
		if err != nil {
			return nil, err
		}
		return map[string]any{"type": "array", "items": item, "minItems": typ.Len(), "maxItems": typ.Len()}, nil
	case *types.Map:
		value, err := g.schema(typ.Elem())
		if err != nil {
			return nil, err
		}
		return map[string]any{"type": "object", "additionalProperties": value}, nil
	case *types.Struct:
		return g.structSchema(typ)
	case *types.Interface:
		return map[string]any{}, nil
	case *types.TypeParam:
		return map[string]any{}, nil
	default:
		return nil, fmt.Errorf("unsupported Go type %T (%s)", typ, types.TypeString(typ, nil))
	}
}

func (g *generator) structSchema(structure *types.Struct) (map[string]any, error) {
	properties := make(map[string]any)
	additionalProperties := any(false)
	hasInlineMap := false

	for index := 0; index < structure.NumFields(); index++ {
		field := structure.Field(index)
		if !field.Exported() {
			continue
		}

		tag := reflect.StructTag(structure.Tag(index)).Get("yaml")
		name, options := parseYAMLTag(tag, field.Name())
		if name == "-" {
			continue
		}

		if options["inline"] {
			inlineType := field.Type()
			for {
				inlineType = types.Unalias(inlineType)
				pointer, ok := inlineType.(*types.Pointer)
				if !ok {
					break
				}
				inlineType = pointer.Elem()
			}
			if named, ok := inlineType.(*types.Named); ok {
				inlineType = named.Underlying()
			}
			switch inlineType := inlineType.(type) {
			case *types.Struct:
				inlineSchema, err := g.structSchema(inlineType)
				if err != nil {
					return nil, err
				}
				for key, value := range inlineSchema["properties"].(map[string]any) {
					if _, exists := properties[key]; exists {
						return nil, fmt.Errorf("duplicate YAML field %q from inline structs", key)
					}
					properties[key] = value
				}
			case *types.Map:
				if hasInlineMap {
					return nil, fmt.Errorf("multiple inline maps are not supported")
				}
				valueSchema, err := g.schema(inlineType.Elem())
				if err != nil {
					return nil, err
				}
				additionalProperties = valueSchema
				hasInlineMap = true
			default:
				return nil, fmt.Errorf("yaml inline field %s must be a struct or map, got %s", field.Name(), types.TypeString(field.Type(), nil))
			}
			continue
		}

		fieldSchema, err := g.schema(field.Type())
		if err != nil {
			return nil, fmt.Errorf("field %s: %w", field.Name(), err)
		}
		if options["omitempty"] {
			fieldSchema["x-yaml-omitempty"] = true
		}
		if _, exists := properties[name]; exists {
			return nil, fmt.Errorf("duplicate YAML field %q", name)
		}
		properties[name] = fieldSchema
	}

	result := map[string]any{"type": "object", "properties": properties}
	if additionalProperties != false {
		result["additionalProperties"] = additionalProperties
	}
	return result, nil
}

func parseYAMLTag(tag, defaultName string) (string, map[string]bool) {
	name := defaultName
	options := make(map[string]bool)
	if tag == "" {
		return strings.ToLower(name), options
	}

	parts := strings.Split(tag, ",")
	if parts[0] != "" {
		name = parts[0]
	} else {
		name = strings.ToLower(name)
	}
	for _, option := range parts[1:] {
		options[option] = true
	}
	return name, options
}

func definitionKey(typ *types.Named) string {
	object := typ.Obj()
	pkgPath := ""
	if object.Pkg() != nil {
		pkgPath = object.Pkg().Path()
	}
	identity := pkgPath + ":" + types.TypeString(typ, func(pkg *types.Package) string {
		return pkg.Path()
	})
	hash := sha256.Sum256([]byte(identity))
	return object.Name() + "_" + hex.EncodeToString(hash[:6])
}

func init() {
	flag.CommandLine.SetOutput(os.Stderr)
	flag.Usage = func() {
		fmt.Fprintf(flag.CommandLine.Output(), "Usage: %s [flags]\n", filepath.Base(os.Args[0]))
		flag.PrintDefaults()
		fmt.Fprintln(flag.CommandLine.Output(), "\nJSON Schema is inferred from Go types and yaml struct tags.")
		fmt.Fprintln(flag.CommandLine.Output(), "Use -from-schema to generate C# models from an existing schema file.")
		fmt.Fprintln(flag.CommandLine.Output(), "Required fields, defaults, and validation rules are not inferred.")
	}
}
