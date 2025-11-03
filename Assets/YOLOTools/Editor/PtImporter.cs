using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "pt")]
public class PtImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        string newPath = ctx.assetPath + ".bytes";

        try
        {
            if (File.Exists(newPath))
            {
                File.Delete(newPath);
            }
            
            File.Move(ctx.assetPath, newPath);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }


    }
}
