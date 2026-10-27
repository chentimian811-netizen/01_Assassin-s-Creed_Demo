/// <summary>
/// UIForm 打开参数容器。UIRouter 只把 userData 原样转交给界面的 OnOpen，
/// 各界面自己从里面取需要的字段；不要为了传一个 int 再造第二套事件总线。
/// </summary>
public sealed class ShopFormArgs
{
    /// <summary>商店表主键（Shop 表的 keeper_id）。对应 ShopNPC 上的 shopKeeperId 字段</summary>
    public int KeeperId;
}
