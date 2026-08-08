using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DLSS_Swapper.Dlls;
using DLSS_Swapper.Extensions;
using DLSS_Swapper.Helpers.FSR31;
using SQLite;

namespace DLSS_Swapper.Data;

[Table("game_asset")]
public class GameAsset : IEquatable<GameAsset>
{
    [Indexed]
    [property: Column("id")]
    public string Id { get; set; } = string.Empty;

    [property: Column("asset_type")]
    public GameAssetType AssetType { get; set; } = GameAssetType.Unknown;

    [property: Column("path")]
    public string Path { get; set; } = string.Empty;

    [property: Column("version")]
    public string Version { get; set; } = string.Empty;

    string _displayVersion = string.Empty;

    [property: Ignore]
    public string DisplayVersion
    {
        get
        {
            // return cached version.
            if (string.IsNullOrWhiteSpace(_displayVersion) == false)
            {
                return _displayVersion;
            }

            if (AssetType == GameAssetType.FSR_31_DX12 || AssetType == GameAssetType.FSR_31_DX12_BACKUP ||
                AssetType == GameAssetType.FSR_31_VK || AssetType == GameAssetType.FSR_31_VK_BACKUP)
            {
                // First try get it from the DLLManager.
                if (AssetType == GameAssetType.FSR_31_DX12 || AssetType == GameAssetType.FSR_31_DX12_BACKUP)
                {
                    var record = DLLManager.Instance.FSR31DX12Records.FirstOrDefault(x => x.MD5Hash == Hash);
                    if (record is not null)
                    {
                        _displayVersion = record.DisplayVersion;
                        return _displayVersion;
                    }
                }
                else
                {
                    var record = DLLManager.Instance.FSR31VKRecords.FirstOrDefault(x => x.MD5Hash == Hash);
                    if (record is not null)
                    {
                        _displayVersion = record.DisplayVersion;
                        return _displayVersion;
                    }
                }

                var latestVersion = FSR31Helper.GetLatestVersion(Path);
                if (string.IsNullOrWhiteSpace(latestVersion) == false)
                {
                    _displayVersion = latestVersion;
                    return _displayVersion;
                }

                // If this isn't loaded we fall back to the existing stuff.
            }

            var version = Version.AsSpan();

            // Remove all the .0's, such that 2.5.0.0 becomes 2.5
            while (version.EndsWith(".0"))
            {
                version = version.Slice(0, version.Length - 2);
            }

            _displayVersion = version.ToString();

            // If the value is a single value, eg 1, make it 1.0
            if (_displayVersion.Contains('.') == false)
            {
                _displayVersion = $"{_displayVersion}.0";
            }

            return _displayVersion;
        }
    }

    string _displayName = string.Empty;

    [property: Ignore]
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_displayName) == false)
            {
                return _displayName;
            }

            if (AssetType == GameAssetType.FSR_31_DX12 || AssetType == GameAssetType.FSR_31_DX12_BACKUP ||
                AssetType == GameAssetType.FSR_31_VK || AssetType == GameAssetType.FSR_31_VK_BACKUP)
            {
                _displayName = $"v{DisplayVersion} (v{Version})";
                return _displayName;
                /*

                var version = Version.AsSpan();

                // Remove all the .0's, such that 2.5.0.0 becomes 2.5
                while (version.EndsWith(".0"))
                {
                    version = version.Slice(0, version.Length - 2);
                }

                var dllVersion = version.ToString();

                // If the value is a single value, eg 1, make it 1.0
                if (dllVersion.Contains(".") == false)
                {
                    dllVersion = $"{dllVersion}.0";
                }

                _displayName = $"v{DisplayVersion} (v{dllVersion})";
                return _displayName;
                */
            }

            _displayName = $"v{DisplayVersion}";
            return _displayName;
        }
    }

    [property: Column("hash")]
    public string Hash { get; set; } = string.Empty;

    public void LoadVersionAndHash()
    {
        if (File.Exists(Path) == false)
        {
            return;
        }

        var fileVersionInfo = FileVersionInfo.GetVersionInfo(Path);
        Version = fileVersionInfo.GetFormattedFileVersion();
        Hash = fileVersionInfo.GetMD5Hash();
    }


    public GameAsset? GetBackup()
    {
        var definition = DllTypes.ForAssetType(AssetType);
        if (definition is null)
        {
            return null;
        }

        var backypType = definition.BackupAssetType;

        var backupPath = Path + ".dlsss";
        if (File.Exists(backupPath) == false)
        {
            return null;
        }

        var backupGameAsset = new GameAsset()
        {
            Id = Id,
            AssetType = backypType,
            Path = backupPath,
        };
        backupGameAsset.LoadVersionAndHash();

        return backupGameAsset;
    }

    public bool Equals(GameAsset? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id.Equals(other.Id) &&
            AssetType.Equals(other.AssetType) &&
            Path.Equals(other.Path) &&
            Version.Equals(other.Version) &&
            Hash.Equals(other.Hash);
    }
}
