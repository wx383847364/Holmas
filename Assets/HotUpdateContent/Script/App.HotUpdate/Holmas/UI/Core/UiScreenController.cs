using App.HotUpdate.Holmas.UI.Binding;
using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    public abstract class UiScreenController
    {
        public UiScreenDefinition Definition { get; private set; }

        public UiScreenService ScreenService { get; private set; }

        public UiRoot Root { get; private set; }

        public UiBindingResolver BindingResolver { get; private set; }

        public UiLoadedPrefabHandle LoadedHandle { get; private set; }

        public GameObject RootObject => LoadedHandle != null ? LoadedHandle.InstanceRoot : null;

        public bool IsOpen { get; private set; }

        protected abstract UiScreenKind DeclaredKind { get; }

        internal void Attach(UiScreenService screenService, UiRoot root, UiLoadedPrefabHandle loadedHandle, UiScreenDefinition definition)
        {
            ScreenService = screenService;
            Root = root;
            LoadedHandle = loadedHandle;
            Definition = definition;
            BindingResolver = new UiBindingResolver(
                loadedHandle != null ? loadedHandle.GetReferenceCollector() : null,
                definition != null ? definition.BindingManifest : null);

            if (definition.Kind != DeclaredKind)
            {
                throw new System.InvalidOperationException(
                    $"UiScreenController: {GetType().Name} 声明语义为 {DeclaredKind}，但定义是 {definition.Kind}。");
            }

            OnCreate();
            OnBind();
        }

        internal void OpenInternal(object payload)
        {
            IsOpen = true;
            LoadedHandle?.SetActive(true);
            OnOpen(payload);
        }

        internal void PauseInternal()
        {
            LoadedHandle?.SetActive(false);
            OnPause();
        }

        internal void ResumeInternal()
        {
            LoadedHandle?.SetActive(true);
            OnResume();
        }

        internal void CloseInternal()
        {
            IsOpen = false;
            OnClose();
            LoadedHandle?.SetActive(false);
        }

        internal void DestroyInternal()
        {
            IsOpen = false;
            OnDestroy();
        }

        protected virtual void OnCreate()
        {
        }

        protected virtual void OnBind()
        {
        }

        protected virtual void OnOpen(object payload)
        {
        }

        protected virtual void OnPause()
        {
        }

        protected virtual void OnResume()
        {
        }

        protected virtual void OnClose()
        {
        }

        protected virtual void OnDestroy()
        {
        }
    }
}
