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
		"NestedConfig NestedValue { get; set; } = new();",
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

func TestGenerateCSharpModelsNamesConfigTypesFromUniquePropertyAndInitializesRootObjects(t *testing.T) {
	schema := schemaDocument{
		"type": "object",
		"properties": map[string]any{
			"logging": map[string]any{
				"$ref": "#/$defs/Config_abc123",
			},
			"rtc": map[string]any{
				"anyOf": []any{
					map[string]any{"$ref": "#/$defs/Config_def456"},
					map[string]any{"type": "null"},
				},
			},
		},
		"$defs": map[string]any{
			"Config_abc123": map[string]any{
				"type": "object",
				"properties": map[string]any{
					"level": map[string]any{"type": "string"},
				},
			},
			"Config_def456": map[string]any{
				"type": "object",
				"properties": map[string]any{
					"use_external_ip": map[string]any{"type": "boolean"},
				},
			},
		},
	}

	generated, err := generateCSharp(schema, "Example.Configuration", "ExampleConfig")
	if err != nil {
		t.Fatal(err)
	}
	output := string(generated)
	for _, expected := range []string{
		"public LoggingConfig Logging { get; set; } = new();",
		"public RtcConfig Rtc { get; set; } = new();",
		"public sealed class LoggingConfig",
		"public sealed class RtcConfig",
	} {
		if !strings.Contains(output, expected) {
			t.Errorf("generated C# does not contain %q:\n%s", expected, output)
		}
	}
}

func TestGenerateCSharpModelsInitializesAllRootObjectReferences(t *testing.T) {
	schema := schemaDocument{
		"type": "object",
		"properties": map[string]any{
			"optional": map[string]any{
				"anyOf": []any{
					map[string]any{"$ref": "#/$defs/OptionalConfig"},
					map[string]any{"type": "null"},
				},
			},
			"required": map[string]any{"$ref": "#/$defs/RequiredConfig"},
			"scalar":   map[string]any{"type": "string"},
		},
		"$defs": map[string]any{
			"OptionalConfig": map[string]any{
				"type":       "object",
				"properties": map[string]any{"value": map[string]any{"type": "string"}},
			},
			"RequiredConfig": map[string]any{
				"type":       "object",
				"properties": map[string]any{"value": map[string]any{"type": "string"}},
			},
		},
	}

	generated, err := generateCSharp(schema, "Example.Configuration", "ExampleConfig")
	if err != nil {
		t.Fatal(err)
	}
	output := string(generated)
	for _, expected := range []string{
		"public OptionalConfig Optional { get; set; } = new();",
		"public RequiredConfig Required { get; set; } = new();",
		"public string? Scalar { get; set; }",
	} {
		if !strings.Contains(output, expected) {
			t.Errorf("generated C# does not contain %q:\n%s", expected, output)
		}
	}
}

func TestGenerateCSharpModelsKeepsHashWhenConfigHasMultiplePropertyNames(t *testing.T) {
	schema := schemaDocument{
		"type": "object",
		"properties": map[string]any{
			"first":  map[string]any{"$ref": "#/$defs/Config_abc123"},
			"second": map[string]any{"$ref": "#/$defs/Config_abc123"},
		},
		"$defs": map[string]any{
			"Config_abc123": map[string]any{
				"type":       "object",
				"properties": map[string]any{"enabled": map[string]any{"type": "boolean"}},
			},
			"Config_def456": map[string]any{
				"type":       "object",
				"properties": map[string]any{"value": map[string]any{"type": "string"}},
			},
		},
	}

	generated, err := generateCSharp(schema, "Example.Configuration", "ExampleConfig")
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(string(generated), "public sealed class Config_abc123") {
		t.Fatalf("ambiguous Config type name should retain its hash:\n%s", generated)
	}
}
