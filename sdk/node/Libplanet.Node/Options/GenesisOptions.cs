using System.ComponentModel;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Node.DataAnnotations;
using Libplanet.Node.DependencyInjection;

namespace Libplanet.Node.Options;

[Options(Position)]
[Description("Options for the genesis block.")]
public sealed class GenesisOptions : OptionsBase<GenesisOptions>
{
    public const string Position = "Genesis";

    public static readonly string AppProtocolToken =
        "200210/AB2da648b9154F2cCcAFBD85e0Bc3d51f97330Fc/MEUCIQCBr..8VdITFe9nMTobl4akFid" +
        ".s8G2zy2pBidAyRXSeAIgER77qX+eywjgyth6QYi7rQw5nK3KXO6cQ6ngUh.CyfU=/ZHU5OnRpbWVzdGFtcHUxMDoyMDI0LTA3LTMwZQ==";

    public static readonly AppProtocolVersion AppProtocolVersion
        = AppProtocolVersion.FromToken(AppProtocolToken);

    [PrivateKey]
    [Description("The key of the genesis block.")]
    public string GenesisKey { get; set; } = string.Empty;

    [PublicKeyArray]
    [Description("Public keys of the validators.")]
    public string[] Validators { get; set; } = [];

    [Description("The timestamp of the genesis block.")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.MinValue;

    [Description("The path of the genesis block.")]
    public string? GenesisBlockPath { get; set; } = "https://release.nine-chronicles.com/genesis-block-9c-main";
}
