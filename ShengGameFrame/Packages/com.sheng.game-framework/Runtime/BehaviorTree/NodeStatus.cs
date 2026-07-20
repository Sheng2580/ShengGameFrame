namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 行为树节点执行状态
    /// </summary>
    public enum NodeStatus
    {
        Invalid,
        Running,
        Success,
        Failure,
        Aborted
    }
}
