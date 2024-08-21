using System.ComponentModel;
using Libplanet.Node.DataAnnotations;
using Libplanet.Node.DependencyInjection;

namespace Libplanet.Node.Options;

[Options(Position, Scope = "Node")]
public sealed class NodeOptions : OptionsBase<NodeOptions>
{
    public const string Position = "Node";

    [PrivateKey]
    [Description("The private key of the node.")]
    public string PrivateKey { get; set; } = string.Empty;

    [BoundPeer]
    [Description("The endpoint of the node to block sync.")]
    public string BlocksyncSeedPeer { get; init; }
        = "027bd36895d68681290e570692ad3736750ceaab37be402442ffb203967f98f7b6,9c-main-tcp-seed-1.planetarium.dev,31234";

    [BoundPeer]
    [Description("The endpoint of the node to consensus.")]
    public string ConsensusSeedPeer { get; init; } = string.Empty;
}
