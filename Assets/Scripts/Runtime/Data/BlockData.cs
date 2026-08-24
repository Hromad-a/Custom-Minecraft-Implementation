using System;

namespace CustomMinecraft
{
    /// <summary>
    /// One cell of the world. Every cell has a deterministic block type assigned
    /// at generation time, even when no block is present; placing a block only
    /// flips <see cref="IsPresent"/> and the type is never chosen by the player.
    /// </summary>
    [Serializable]
    public struct BlockData : IEquatable<BlockData>
    {
        public bool IsPresent;
        public int BlockTypeId;

        public BlockData(bool isPresent, int blockTypeId)
        {
            IsPresent = isPresent;
            BlockTypeId = blockTypeId;
        }

        public bool Equals(BlockData other) =>
            IsPresent == other.IsPresent && BlockTypeId == other.BlockTypeId;

        public override bool Equals(object obj) => obj is BlockData other && Equals(other);

        public override int GetHashCode() => (BlockTypeId << 1) | (IsPresent ? 1 : 0);

        public override string ToString() =>
            IsPresent ? $"Block(type {BlockTypeId})" : $"Air(type {BlockTypeId})";
    }
}
