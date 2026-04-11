using System.Threading.Tasks;
using App.Shared.Holmas.RuntimeData;

namespace App.HotUpdate.Holmas.UI.Core
{
    public interface IBattleWorldHost
    {
        Task PrepareAsync(LevelSnapshot snapshot);
        void Show();
        void Hide();
        void Release();
    }
}
