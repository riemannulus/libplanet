using Libplanet.Types.Blocks;

namespace Libplanet.Node.Services;

internal sealed class ReadChainService(BlockChainService blockChainService) : IReadChainService
{
    public Block Tip => blockChainService.BlockChain.Tip;

    public Block GetBlock(BlockHash hash) => blockChainService.BlockChain[hash];

    public Block GetBlock(long height) => blockChainService.BlockChain[height];
}
