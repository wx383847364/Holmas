using System.IO;
using App.HotUpdate.Holmas.Tasks.Config;
using Holmas.EditorTools;
using NUnit.Framework;

namespace Holmas.EditorTests
{
    public sealed class HolmasCsvBinaryExporterTests
    {
        [Test]
        public void HolmasCsvBinaryExporter_ExportsReadablePackages()
        {
            HolmasCsvExportReport report = HolmasCsvBinaryExporter.ExportAll();

            Assert.That(report, Is.Not.Null);
            Assert.That(report.Success, Is.True, string.Join("\n", report.Errors));

            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string corePath = Path.Combine(projectRoot, "Assets/HotUpdateContent/Config/holmas_core_config.bytes");
            string catPath = Path.Combine(projectRoot, "Assets/HotUpdateContent/Config/holmas_cat_meta.bytes");
            string reportPath = Path.Combine(projectRoot, "Assets/Config/json/holmas_export_report.json");

            Assert.That(File.Exists(corePath), Is.True, corePath);
            Assert.That(File.Exists(catPath), Is.True, catPath);
            Assert.That(File.Exists(reportPath), Is.True, reportPath);

            bool success = HolmasConfigCatalogFactory.TryCreateFromBinary(
                File.ReadAllBytes(corePath),
                File.ReadAllBytes(catPath),
                out HolmasConfigCatalogBundle bundle,
                out HolmasConfigReport runtimeReport);

            Assert.That(success, Is.True, runtimeReport == null ? "runtime report missing" : string.Join("\n", runtimeReport.Errors));
            Assert.That(bundle, Is.Not.Null);
            Assert.That(bundle.Report.Errors, Is.Empty);
            Assert.That(bundle.Cats.Count, Is.GreaterThan(0));
            Assert.That(bundle.Maps.Count, Is.GreaterThan(0));
            Assert.That(bundle.TaskTemplates.Count, Is.GreaterThan(0));
            Assert.That(bundle.PlayerLevels.Count, Is.GreaterThan(0));
        }
    }
}
