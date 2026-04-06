using System;
using UiPrefabGenerator.Core.Interpretation;
using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Intake
{
    public sealed class DefaultDesignPacketToUiPrefabSpecInterpreter : IUiSpecInterpreter
    {
        private readonly IDesignPacketIntakeAnalyzer _analyzer;
        private readonly string _generationProfileId;

        public DefaultDesignPacketToUiPrefabSpecInterpreter(
            IDesignPacketIntakeAnalyzer analyzer = null,
            string generationProfileId = "holmas_ugui")
        {
            _analyzer = analyzer ?? new DefaultDesignPacketIntakeAnalyzer();
            _generationProfileId = string.IsNullOrWhiteSpace(generationProfileId) ? "holmas_ugui" : generationProfileId;
        }

        public UiPrefabSpec Interpret(DesignPacket designPacket)
        {
            DesignPacketIntakeAssessment assessment = _analyzer.Analyze(designPacket);
            if (assessment.HasBlockingIssues)
            {
                throw new InvalidOperationException("DesignPacket 存在 blocking intake issues，不能直接转换为 UiPrefabSpec。");
            }

            var spec = new UiPrefabSpec
            {
                PageId = designPacket.PageId ?? string.Empty,
                PrefabName = designPacket.PrefabName ?? string.Empty,
                RootNodeId = "root",
                GenerationProfileId = _generationProfileId,
            };

            var rootNode = new UiNodeSpec
            {
                NodeId = "root",
                NodeName = designPacket.PrefabName ?? string.Empty,
                ParentNodeId = string.Empty,
                Layout = new UiLayoutSpec
                {
                    LayoutType = "FullScreen",
                    LayoutSlot = "root",
                },
            };
            rootNode.Components.Add(new UiComponentSpec
            {
                ComponentType = "RectTransform",
            });

            string primaryAssetSlot = ResolvePrimaryAssetSlot(designPacket);
            if (!string.IsNullOrWhiteSpace(primaryAssetSlot))
            {
                rootNode.Components.Add(new UiComponentSpec
                {
                    ComponentType = "Image",
                    AssetSlot = primaryAssetSlot,
                });
            }

            spec.Nodes.Add(rootNode);
            return spec;
        }

        private static string ResolvePrimaryAssetSlot(DesignPacket designPacket)
        {
            if (designPacket == null || designPacket.AssetSlotHints == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < designPacket.AssetSlotHints.Count; i++)
            {
                DesignAssetSlotHint hint = designPacket.AssetSlotHints[i];
                if (hint != null && !string.IsNullOrWhiteSpace(hint.SlotId))
                {
                    return hint.SlotId;
                }
            }

            return string.Empty;
        }
    }
}
