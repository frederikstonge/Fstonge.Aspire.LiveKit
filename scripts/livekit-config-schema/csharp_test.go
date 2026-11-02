package main

import (
	"strings"
	"testing"
)

func TestGenerateCSharpModelsUsesYamlAliasesAndSchemaTypes(t *testing.T) {
	schema := schemaDocument{
		"type":  "object",
		"title": "Config",
		"properties": map[string]any{
			"nested_value": map[string]any{
				"$ref": "#/$defs/NestedConfig_abc123",
			},
			"retry_count": map[string]any{
				"type": "integer",
			},
			"optional_items": map[string]any{
				"type": "array",
				"items": map[string]any{
					"anyOf": []any{
						map[string]any{"$ref": "#/$defs/NestedConfig_abc123"},
						map[string]any{"type": "null"},
					},
				},
			},
		},
		"$defs": map[string]any{
			"NestedConfig_abc123": map[string]any{
				"type": "object",
				"properties": map[string]any{
					"wire_name": map[string]any{"type": "string"},
				},
			},
			"State_abc123": map[string]any{
				"type": "string",
			},
		},
	}

	generated, err := generateCSharp(schema, "Example.Configuration", "ExampleConfig")
	if err != nil {
		t.Fatal(err)
	}
	output := string(generated)
	for _, expected := range []string{
		"namespace Example.Configuration;",
		"public sealed class ExampleConfig",
		`[YamlMember(Alias = "nested_value")]`,
		"NestedConfig? NestedValue",
		"long? RetryCount",
		"List<NestedConfig?>? OptionalItems",
		`[YamlMember(Alias = "wire_name")]`,
		"public string? WireName",
	} {
		if !strings.Contains(output, expected) {
			t.Errorf("generated C# does not contain %q:\n%s", expected, output)
		}
	}
}

func TestGenerateCSharpModelsRejectsUnresolvedReferences(t *testing.T) {
	schema := schemaDocument{
		"type": "object",
		"properties": map[string]any{
			"missing": map[string]any{"$ref": "#/$defs/Missing"},
		},
		"$defs": map[string]any{},
	}

	if _, err := generateCSharp(schema, "Example.Configuration", "ExampleConfig"); err == nil {
		t.Fatal("expected unresolved schema reference to fail")
	}
}
