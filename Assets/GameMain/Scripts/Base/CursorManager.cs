using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorManager
{
    private static CursorManager _instance;
    public static CursorManager Instance => _instance ?? (_instance = new CursorManager());

    public bool IsGameplayFocused => _unlockReasons.Count == 0;
    private HashSet<string> _unlockReasons = new HashSet<string>();

    private CursorManager()
    {
        
    }

    public void AddLock(string reason)
    {
        _unlockReasons.Add(reason);
        Apply();
    }

    public void RemoveLock(string reason)
    {
        _unlockReasons.Remove(reason);
        Apply();
    }

    public void HoldCursor() => AddLock("AltHold");
    public void ReleaseCursor() => RemoveLock("AltHold");

    private void Apply()
    {
        if (_unlockReasons.Count > 0)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        CameraManager.Instance?.SetLookEnabled(IsGameplayFocused);
    }
}
