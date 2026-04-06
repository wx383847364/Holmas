using UiPrefabGenerator.Core.Schema;

namespace UiPrefabGenerator.Core.Interpretation
{
    public interface IUiSpecInterpreter
    {
        UiPrefabSpec Interpret(DesignPacket designPacket);
    }
}
