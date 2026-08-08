using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DLSS_Swapper.Data;

public class KnownDLLs
{
    [JsonPropertyName("dlss")]
    public List<HashedKnownDLL> DLSS { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("dlss_d")]
    public List<HashedKnownDLL> DLSS_D { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("dlss_g")]
    public List<HashedKnownDLL> DLSS_G { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("fsr_31_dx12")]
    public List<HashedKnownDLL> FSR_31_DX12 { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("fsr_31_vk")]
    public List<HashedKnownDLL> FSR_31_VK { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("xess")]
    public List<HashedKnownDLL> XeSS { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("xell")]
    public List<HashedKnownDLL> XeLL { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("xess_fg")]
    public List<HashedKnownDLL> XeSS_FG { get; set; } = new List<HashedKnownDLL>();

    [JsonPropertyName("xess_dx11")]
    public List<HashedKnownDLL> XeSS_DX11 { get; set; } = new List<HashedKnownDLL>();

    /// <summary>
    /// The known hashes for an asset type.
    /// </summary>
    /// <remarks>
    /// As with Manifest, the named properties exist because these arrive under fixed json keys. This
    /// is the one place mapping an asset type onto them, and it goes away when this becomes keyed.
    /// </remarks>
    public List<HashedKnownDLL>? GetHashes(GameAssetType assetType)
    {
        // NOTE: DLL type
        return assetType switch
        {
            GameAssetType.DLSS => DLSS,
            GameAssetType.DLSS_D => DLSS_D,
            GameAssetType.DLSS_G => DLSS_G,
            GameAssetType.FSR_31_DX12 => FSR_31_DX12,
            GameAssetType.FSR_31_VK => FSR_31_VK,
            GameAssetType.XeSS => XeSS,
            GameAssetType.XeLL => XeLL,
            GameAssetType.XeSS_FG => XeSS_FG,
            GameAssetType.XeSS_DX11 => XeSS_DX11,
            _ => null,
        };
    }
}
