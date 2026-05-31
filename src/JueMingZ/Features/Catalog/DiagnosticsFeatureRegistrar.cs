using JueMingZ.Actions;
using JueMingZ.Common;

namespace JueMingZ.Features.Catalog
{
    public static class DiagnosticsFeatureRegistrar
    {
        public static void Register(FeatureRegistry registry)
        {
            registry.Register(FeatureDefinitionBuilder
                .Create("diagnostics.health_check", "健康检查", "M0/M1/M2/M3 用于确认框架、配置、日志、动作队列和诊断快照可用。")
                .Domain(FeatureCodeDomain.Diagnostics)
                .Category(FeatureUserCategory.Diagnostics)
                .Actions(InputActionKind.DiagnosticNoop)
                .GameState(GameStateKind.None)
                .Multiplayer(FeatureMultiplayerSupport.SupportedByOriginalAction)
                .DefaultEnabled(true)
                .VisibleInMainUi(false)
                .Implemented(true)
                .InternalPlatform(true)
                .Notes("内置健康检查，不实现任何游戏功能。")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(
                    FeatureIds.DiagnosticsWorldGenDebugViewer,
                    "WorldGen Debug Viewer",
                    "Enables the hidden vanilla Terraria WorldGen Debug / Worldgen Viewer entry when the original field exists.")
                .Domain(FeatureCodeDomain.Diagnostics)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.None)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .DefaultEnabled(true)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("Runtime-only reflection patch for the vanilla world-generation F5 details window. It is enabled by default and the Misc UI row is informational only; after LateBootstrap it sets the shared vanilla enableDebugCommands field and installs localization hooks. It adds no JueMing-Z debug command UI and skips safely when the field is missing.")
                .Build());

            registry.Register(FeatureDefinitionBuilder
                .Create(
                    FeatureIds.DiagnosticsDeveloperDebugCommands,
                    "开发者菜单",
                    "Exposes the JueMing-Z F5 Misc button that opens vanilla /hh debug command help after confirmation.")
                .Domain(FeatureCodeDomain.Diagnostics)
                .Category(FeatureUserCategory.Misc)
                .Actions(InputActionKind.None)
                .GameState(GameStateKind.None)
                .Multiplayer(FeatureMultiplayerSupport.Unknown)
                .DefaultEnabled(true)
                .VisibleInMainUi(true)
                .Implemented(true)
                .Notes("Exposes only the JueMing-Z Misc page button that opens the vanilla /hh command list after a confirmation click. It no longer has a persistent startup switch, and it does not add JueMing-Z debug command UI.")
                .Build());
        }
    }
}
