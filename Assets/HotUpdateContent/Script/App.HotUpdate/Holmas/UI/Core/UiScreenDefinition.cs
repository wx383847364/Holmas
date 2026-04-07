using System;

namespace App.HotUpdate.Holmas.UI.Core
{
    public sealed class UiScreenDefinition
    {
        public UiScreenDefinition(string id, string assetAddress, UiScreenKind kind, Type controllerType)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("UiScreenDefinition: id 不能为空。", nameof(id));
            }

            if (controllerType == null)
            {
                throw new ArgumentNullException(nameof(controllerType));
            }

            Id = id;
            AssetAddress = assetAddress ?? string.Empty;
            Kind = kind;
            ControllerType = controllerType;
            Layer = kind.ToString();
            CachePolicy = UiCachePolicy.DestroyOnClose;
        }

        public string Id { get; }

        public string AssetAddress { get; }

        public UiScreenKind Kind { get; }

        public Type ControllerType { get; }

        public string Layer { get; set; }

        public UiCachePolicy CachePolicy { get; set; }

        public bool ClickOutsideToClose { get; set; }

        public bool BlockInputDuringTransition { get; set; }

        public bool PreloadOnBootstrap { get; set; }

        public string SheetGroupId { get; set; }

        public bool Exclusive { get; set; }
    }
}
