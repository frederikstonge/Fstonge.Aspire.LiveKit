package main

import (
	"go/token"
	"go/types"
	"testing"
)

func TestStructSchemaFlattensInlineTaggedType(t *testing.T) {
	pkg := types.NewPackage("example/config", "config")
	credentials := types.NewNamed(
		types.NewTypeName(token.NoPos, pkg, "Credentials", nil),
		types.NewStruct(
			[]*types.Var{
				types.NewField(token.NoPos, pkg, "APIKey", types.Typ[types.String], false),
			},
			[]string{`yaml:"api_key"`},
		),
		nil,
	)
	configuration := types.NewStruct(
		[]*types.Var{
			types.NewField(token.NoPos, pkg, "Credentials", credentials, true),
			types.NewField(token.NoPos, pkg, "HealthPort", types.Typ[types.Int], false),
		},
		[]string{`yaml:",inline"`, `yaml:"health_port,omitempty"`},
	)

	schema, err := (&generator{definitions: make(map[string]any)}).structSchema(configuration)
	if err != nil {
		t.Fatal(err)
	}

	properties := schema["properties"].(map[string]any)
	if got := properties["api_key"].(map[string]any)["type"]; got != "string" {
		t.Fatalf("api_key type = %v, want string", got)
	}
	healthPort := properties["health_port"].(map[string]any)
	if got := healthPort["type"]; got != "integer" {
		t.Fatalf("health_port type = %v, want integer", got)
	}
	if got := healthPort["x-yaml-omitempty"]; got != true {
		t.Fatalf("health_port omitempty = %v, want true", got)
	}
}

func TestSchemaMarksPointersNullable(t *testing.T) {
	schema, err := (&generator{definitions: make(map[string]any)}).schema(
		types.NewPointer(types.Typ[types.String]),
	)
	if err != nil {
		t.Fatal(err)
	}

	alternatives := schema["anyOf"].([]any)
	if got := alternatives[0].(map[string]any)["type"]; got != "string" {
		t.Fatalf("pointer value type = %v, want string", got)
	}
	if got := alternatives[1].(map[string]any)["type"]; got != "null" {
		t.Fatalf("pointer nullable type = %v, want null", got)
	}
}

func TestSchemaTreatsTimeDurationAsString(t *testing.T) {
	pkg := types.NewPackage("time", "time")
	duration := types.NewNamed(
		types.NewTypeName(token.NoPos, pkg, "Duration", nil),
		types.Typ[types.Int64],
		nil,
	)

	schema, err := (&generator{definitions: make(map[string]any)}).schema(duration)
	if err != nil {
		t.Fatal(err)
	}
	if got := schema["format"]; got != "duration" {
		t.Fatalf("duration format = %v, want duration", got)
	}
	if got := schema["type"]; got != "string" {
		t.Fatalf("duration type = %v, want string", got)
	}
}
