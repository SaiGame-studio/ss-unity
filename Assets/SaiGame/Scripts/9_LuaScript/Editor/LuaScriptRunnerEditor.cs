using UnityEditor;

namespace SaiGame.Services
{
    [CustomEditor(typeof(LuaScriptRunner))]
    [CanEditMultipleObjects]
    public sealed class LuaScriptRunnerEditor : BattleScriptEditor
    {
    }
}
