using NUnit.Framework;
using UiPrefabGenerator.Core.Schema;
using UiPrefabGenerator.Core.Validation;

namespace UiPrefabGenerator.Tests.EditMode
{
    public sealed class UiPrefabSpecValidatorTests
    {
        [Test]
        public void Validate_Fails_WhenRootNodeMissing()
        {
            var spec = new UiPrefabSpec
            {
                PageId = "agency_main",
                PrefabName = "AgencyMainPanel",
                RootNodeId = "root",
            };
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "content",
                NodeName = "Content",
                ParentNodeId = string.Empty,
            });

            var result = new DefaultUiSpecValidator().Validate(spec);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("RootNodeId"));
        }

        [Test]
        public void Validate_Fails_WhenNodeIdsDuplicate()
        {
            var spec = new UiPrefabSpec
            {
                PageId = "agency_main",
                PrefabName = "AgencyMainPanel",
                RootNodeId = "root",
            };
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "root",
                NodeName = "Root",
            });
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "root",
                NodeName = "RootDuplicate",
                ParentNodeId = "root",
            });

            var result = new DefaultUiSpecValidator().Validate(spec);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("重复 NodeId"));
        }

        [Test]
        public void Validate_Fails_WhenParentGraphContainsCycle()
        {
            var spec = new UiPrefabSpec
            {
                PageId = "agency_main",
                PrefabName = "AgencyMainPanel",
                RootNodeId = "root",
            };
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "root",
                NodeName = "Root",
                ParentNodeId = "content",
            });
            spec.Nodes.Add(new UiNodeSpec
            {
                NodeId = "content",
                NodeName = "Content",
                ParentNodeId = "root",
            });

            var result = new DefaultUiSpecValidator().Validate(spec);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("父子环"));
        }
    }
}
