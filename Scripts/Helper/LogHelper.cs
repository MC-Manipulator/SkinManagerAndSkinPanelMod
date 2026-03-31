using MegaCrit.Sts2.Core.Logging;
using SkinManagerAndSkinPanelMod.Scripts.Data;

namespace SkinManagerAndSkinPanelMod.Scripts.Helper;

public static class LogHelper
{
    private const string Prefix = "[皮肤管理器]";
    
    
    public static void LogLoadPath(string loadItem, string path, SkinData skinData)
    {
        BaseLog("读取" + loadItem + "路径 : " + skinData.SkinId + " : " + path);
    }
    
    public static void LogNoneSkin()
    {
        BaseLog("未读取到皮肤");
    }
    
    public static void LogEmptyPath(string loadItem, SkinData skinData)
    {
        BaseLog("读取" + loadItem + "路径为空: " + skinData.SkinId);
    }
    
    public static void LogLoad(string loadItem, SkinData skinData)
    {
        BaseLog("加载" + loadItem + " : " + skinData.SkinId);
    }
    
    public static void LogReplace(string replaceItem, SkinData skinData)
    {
        BaseLog("正在替换" + replaceItem + " : " + skinData.SkinId);
    }

    public static void ErrorLoad(string loadItem, SkinData skinData)
    {
        BaseError("未能成功加载" + loadItem + " : " + skinData.SkinId);
    }

    public static void BaseLog(string msg)
    {
        Log.Info(Prefix + " " + msg);
    }
    
    public static void BaseWarn(string msg)
    {
        Log.Warn(Prefix + " " + msg);
    }
    
    public static void BaseError(string msg)
    {
        Log.Error(Prefix + " " + msg);
    }
}