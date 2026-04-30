using NUnit.Framework;
using UiPrefabGenerator.Core.Profile;
using UiPrefabGenerator.Editor.Bridge;
using UiPrefabGenerator.Editor.Template;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiGenerationTemplateStoreTests
    {
        [Test]
        public void BuildPortraitWechatDefault_ReturnsExpectedPortraitDefaults()
        {
            UiGenerationTemplate template = UiGenerationTemplateStore.BuildPortraitWechatDefault();

            Assert.That(template.TemplateName, Is.EqualTo("holmas_portrait_wechat_default"));
            Assert.That(template.ProfileId, Is.EqualTo("holmas_ugui_portrait"));
            Assert.That(template.TargetPlatform, Is.EqualTo("wechat_minigame"));
            Assert.That(template.Orientation, Is.EqualTo("portrait"));
            Assert.That(template.ReferenceResolutionWidth, Is.EqualTo(1080));
            Assert.That(template.ReferenceResolutionHeight, Is.EqualTo(1920));
            Assert.That(template.DraftPrefabRoot, Is.EqualTo("Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait"));
        }

        [Test]
        public void EnsureDefaultTemplateExists_WritesLoadableTemplateFile()
        {
            UiGenerationTemplateStore.EnsureDefaultTemplateExists();
            UiGenerationTemplate template = UiGenerationTemplateStore.LoadTemplate(UiGenerationDataPaths.DefaultPortraitTemplatePath);

            Assert.That(template, Is.Not.Null);
            Assert.That(template.ProfileId, Is.EqualTo("holmas_ugui_portrait"));
            Assert.That(template.Orientation, Is.EqualTo("portrait"));
        }
    }
}
