﻿using System.Globalization;
using Microsoft.CodeAnalysis;

namespace SyncFaction.Extras;

[Generator]
public class BuildTimeGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var typeName = "GeneratedBuildTime";

        var source = $@"// <auto-generated/>
using System;

namespace SyncFaction.Extras
{{
    public static class {typeName}
    {{
        public static string GetValue() =>
            ""{DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}"";
    }}
}}
";
        context.AddSource($"{typeName}.g.cs", source);
    }
}
