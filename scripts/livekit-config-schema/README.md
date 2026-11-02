# LiveKit Go configuration schema generator

This tool loads a Go package, starts at a named configuration type, follows
referenced Go types (including types from imported modules), and writes a
JSON Schema using the `yaml` struct tags.

Run it from this directory with Go 1.23 or later and the upstream repositories
checked out locally:

```powershell
go run . -dir C:\src\livekit -package ./pkg/config -type Config -out server.schema.json
go run . -dir C:\src\ingress -package ./pkg/config -type Config -out ingress.schema.json
go run . -dir C:\src\egress -package ./pkg/config -type ServiceConfig -out egress.schema.json
go run . -dir C:\src\sip -package ./pkg/config -type Config -out sip.schema.json
```

These are the current root configuration types accepted by the services'
configuration loaders. Generate C# models from a saved schema with:

```powershell
go run . -from-schema C:\src\server.schema.json `
  -csharp-out C:\src\LiveKitServerConfiguration.g.cs `
  -csharp-namespace Aspire.Hosting.LiveKit.Configuration.Server `
  -csharp-root-type LiveKitServerUpstreamConfiguration
```

The generated classes use YamlDotNet `YamlMember` aliases, so they can be
serialized and deserialized with YamlDotNet:

```csharp
var serializer = new SerializerBuilder()
    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
    .Build();
var yaml = serializer.Serialize(configuration);
```

Root properties that reference generated object definitions are automatically
instantiated as non-nullable properties, so nested configuration is directly
available without manually constructing each section. Empty nested mappings
are removed when Aspire serializes configuration. Hash-suffixed `Config`
definitions with a unique referring property are named from that property (for
example `LoggingConfig`). Ambiguous references retain their disambiguated
Go-derived name.

The upstream refresh script generates each schema first, validates it, and
then uses that schema file as input for C# model generation.

The generator handles scalar, pointer, slice, array, map, struct, named, and
embedded `yaml:",inline"` types, and emits reusable named types under `$defs`.
It fails when the package cannot be loaded, when a type reachable from the root
cannot be resolved, or when it encounters unsupported inline fields or
duplicate YAML keys. Go type-check diagnostics in unrelated package code are
reported as warnings; they do not prevent generation when all reachable config
types are resolved. Generated schema unions with multiple non-null branches map
to `object?`, allowing YamlDotNet to preserve the concrete runtime value.

Go types and YAML tags do not capture the whole configuration contract. This
tool does not infer required fields, defaults, validation rules, environment
variable behavior, or custom YAML unmarshalling semantics. Pointer fields are
nullable; all other fields are optional because YAML tags alone do not indicate
requiredness. Review generated schemas against upstream docs and examples.
