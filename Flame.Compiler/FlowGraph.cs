using System.Collections.Generic;
using Flame.Collections;
using Flame.Compiler.Flow;
using System.Collections.Immutable;

namespace Flame.Compiler
{
    /// <summary>
    /// An immutable control-flow graph that consists of basic blocks.
    /// </summary>
    public sealed class FlowGraph
    {
        /// <summary>
        /// Creates a control-flow graph that contains only an empty
        /// entry point block.
        /// </summary>
        public FlowGraph()
        {
            this.instructions = ImmutableDictionary.Create<ValueTag, Instruction>();
            this.blocks = ImmutableDictionary.Create<BasicBlockTag, BasicBlockData>();
            this.blockParamTypes = ImmutableDictionary.Create<ValueTag, IType>();
            this.valueParents = ImmutableDictionary.Create<ValueTag, BasicBlockTag>();
            this.EntryPointTag = new BasicBlockTag("entry-point");
            this.blocks = this.blocks.SetItem(
                this.EntryPointTag,
                new BasicBlockData());
        }

        private FlowGraph(
            FlowGraph other)
        {
            this.instructions = other.instructions;
            this.blocks = other.blocks;
            this.blockParamTypes = other.blockParamTypes;
            this.valueParents = other.valueParents;
            this.EntryPointTag = other.EntryPointTag;
        }

        private ImmutableDictionary<ValueTag, Instruction> instructions;
        private ImmutableDictionary<BasicBlockTag, BasicBlockData> blocks;
        private ImmutableDictionary<ValueTag, IType> blockParamTypes;
        private ImmutableDictionary<ValueTag, BasicBlockTag> valueParents;

        /// <summary>
        /// Gets the tag of the entry point block.
        /// </summary>
        /// <returns>The tag of the entry point block.</returns>
        public BasicBlockTag EntryPointTag { get; private set; }

        /// <summary>
        /// Creates a new basic block that includes all basic blocks in this
        /// graph plus an empty basic block. The latter basic block is returned.
        /// </summary>
        /// <param name="name">The (preferred) name of the basic block's tag.</param>
        /// <returns>An empty basic block in a new control-flow graph.</returns>
        public BasicBlock AddBasicBlock(string name)
        {
            var tag = new BasicBlockTag(name);
            var data = new BasicBlockData();

            var newGraph = new FlowGraph(this);
            newGraph.blocks = newGraph.blocks.Add(tag, data);
            return new BasicBlock(newGraph, tag, data);
        }

        /// <summary>
        /// Removes the basic block with a particular tag from this
        /// control-flow graph.
        /// </summary>
        /// <param name="tag">The basic block's tag.</param>
        /// <returns>
        /// A new control-flow graph that does not contain the basic block.
        /// </returns>
        public FlowGraph RemoveBasicBlock(BasicBlockTag tag)
        {
            AssertContainsBasicBlock(tag);

            var newGraph = new FlowGraph(this);

            var oldData = blocks[tag];
            var oldParams = oldData.Parameters;
            var oldInsns = oldData.InstructionTags;

            var paramTypeBuilder = newGraph.blockParamTypes.ToBuilder();
            var valueParentBuilder = newGraph.valueParents.ToBuilder();

            int oldParamCount = oldParams.Count;
            for (int i = 0; i < oldParamCount; i++)
            {
                paramTypeBuilder.Remove(oldParams[i].Tag);
                valueParentBuilder.Remove(oldParams[i].Tag);
            }

            valueParentBuilder.RemoveRange(oldInsns);

            newGraph.blockParamTypes = paramTypeBuilder.ToImmutable();
            newGraph.valueParents = valueParentBuilder.ToImmutable();
            newGraph.instructions = newGraph.instructions.RemoveRange(oldInsns);
            newGraph.blocks = newGraph.blocks.Remove(tag);

            return newGraph;
        }

        /// <summary>
        /// Creates a new control-flow graph that takes the basic block
        /// with a particular tag as entry point.
        /// </summary>
        /// <param name="tag">The tag of the new entry point block.</param>
        /// <returns>A control-flow graph.</returns>
        public FlowGraph WithEntryPoint(BasicBlockTag tag)
        {
            AssertContainsBasicBlock(tag);
            var newGraph = new FlowGraph(this);
            newGraph.EntryPointTag = tag;
            return newGraph;
        }

        /// <summary>
        /// Gets the basic block with a particular tag.
        /// </summary>
        /// <param name="tag">The basic block's tag.</param>
        /// <returns>A basic block.</returns>
        public BasicBlock GetBasicBlock(BasicBlockTag tag)
        {
            return new BasicBlock(this, tag, blocks[tag]);
        }

        /// <summary>
        /// Checks if this control-flow graph contains a basic block
        /// with a particular tag.
        /// </summary>
        /// <param name="tag">The basic block's tag.</param>
        /// <returns>
        /// <c>true</c> if this control-flow graph contains a basic block
        /// with the given tag; otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsBasicBlock(BasicBlockTag tag)
        {
            return blocks.ContainsKey(tag);
        }

        /// <summary>
        /// Checks if this control-flow graph contains an instruction
        /// with a particular tag.
        /// </summary>
        /// <param name="tag">The instruction's tag.</param>
        /// <returns>
        /// <c>true</c> if this control-flow graph contains an instruction
        /// with the given tag; otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsInstruction(ValueTag tag)
        {
            return instructions.ContainsKey(tag);
        }

        /// <summary>
        /// Checks if this control-flow graph contains a basic block parameter
        /// with a particular tag.
        /// </summary>
        /// <param name="tag">The parameter's tag.</param>
        /// <returns>
        /// <c>true</c> if this control-flow graph contains a basic block parameter
        /// with the given tag; otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsBlockParameter(ValueTag tag)
        {
            return blockParamTypes.ContainsKey(tag);
        }

        /// <summary>
        /// Checks if this control-flow graph contains an instruction
        /// or basic block parameter with a particular tag.
        /// </summary>
        /// <param name="tag">The value's tag.</param>
        /// <returns>
        /// <c>true</c> if this control-flow graph contains a value
        /// with the given tag; otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsValue(ValueTag tag)
        {
            return ContainsInstruction(tag)
                || ContainsBlockParameter(tag);
        }

        /// <summary>
        /// Gets the type of a value in this graph.
        /// </summary>
        /// <param name="tag">The value's type.</param>
        /// <returns></returns>
        public IType GetValueType(ValueTag tag)
        {
            AssertContainsValue(tag);
            Instruction instr;
            if (instructions.TryGetValue(tag, out instr))
            {
                return instr.Type;
            }
            else
            {
                return blockParamTypes[tag];
            }
        }

        /// <summary>
        /// Gets basic block that defines a value with a
        /// particular tag.
        /// </summary>
        /// <param name="tag">The tag of the value to look for.</param>
        /// <returns>The basic block that defines the value.</returns>
        public BasicBlock GetValueParent(ValueTag tag)
        {
            AssertContainsValue(tag);
            return GetBasicBlock(valueParents[tag]);
        }

        /// <summary>
        /// Asserts that this control-flow graph must contain a basic block
        /// with a particular tag.
        /// </summary>
        /// <param name="tag">
        /// The tag of the basic block that must be in the graph.
        /// </param>
        /// <param name="message">
        /// The error message for when no basic block in this control-flow graph
        /// has the tag.
        /// </param>
        public void AssertContainsBasicBlock(BasicBlockTag tag, string message)
        {
            ContractHelpers.Assert(ContainsBasicBlock(tag), message);
        }

        /// <summary>
        /// Asserts that this control-flow graph must contain a basic block
        /// with a particular tag.
        /// </summary>
        /// <param name="tag">
        /// The tag of the basic block that must be in the graph.
        /// </param>
        public void AssertContainsBasicBlock(BasicBlockTag tag)
        {
            AssertContainsBasicBlock(tag, "The graph does not contain the given basic block.");
        }

        /// <summary>
        /// Asserts that this control-flow graph must contain an instruction
        /// or basic block parameter with a particular tag.
        /// </summary>
        /// <param name="tag">
        /// The tag of the value that must be in the graph.
        /// </param>
        /// <param name="message">
        /// The error message for when no value in this control-flow graph
        /// has the tag.
        /// </param>
        public void AssertContainsValue(ValueTag tag, string message)
        {
            ContractHelpers.Assert(ContainsValue(tag), message);
        }

        /// <summary>
        /// Asserts that this control-flow graph must contain an instruction
        /// or basic block parameter with a particular tag.
        /// </summary>
        /// <param name="tag">
        /// The tag of the value that must be in the graph.
        /// </param>
        public void AssertContainsValue(ValueTag tag)
        {
            AssertContainsValue(tag, "The graph does not contain the given value.");
        }

        internal BasicBlock UpdateBasicBlockFlow(BasicBlockTag tag, BlockFlow flow)
        {
            AssertContainsBasicBlock(tag);
            var oldBlock = blocks[tag];

            var newData = new BasicBlockData(
                oldBlock.Parameters,
                oldBlock.InstructionTags,
                flow);

            var newGraph = new FlowGraph(this);
            newGraph.blocks = newGraph.blocks.SetItem(tag, newData);

            return new BasicBlock(newGraph, tag, newData);
        }

        internal BasicBlock UpdateBasicBlockParameters(
            BasicBlockTag tag,
            ImmutableList<BlockParameter> parameters)
        {
            AssertContainsBasicBlock(tag);
            var oldBlock = blocks[tag];

            var newData = new BasicBlockData(
                parameters,
                oldBlock.InstructionTags,
                oldBlock.Flow);

            var oldData = blocks[tag];
            var oldParams = oldData.Parameters;

            var newGraph = new FlowGraph(this);

            var paramTypeBuilder = newGraph.blockParamTypes.ToBuilder();
            var valueParentBuilder = newGraph.valueParents.ToBuilder();

            // Remove the basic block's parameters from the value parent
            // and parameter type dictionaries.
            int oldParamCount = oldParams.Count;
            for (int i = 0; i < oldParamCount; i++)
            {
                paramTypeBuilder.Remove(oldParams[i].Tag);
                valueParentBuilder.Remove(oldParams[i].Tag);
            }

            // Add the new basic block parameters to the value parent and
            // parameter type dictionaries.
            int newParamCount = parameters.Count;
            for (int i = 0; i < newParamCount; i++)
            {
                var item = parameters[i];

                ContractHelpers.Assert(
                    !valueParentBuilder.ContainsKey(item.Tag),
                    "Value tag '" + item.Tag.Name + "' cannot appear twice in the same control-flow graph.");

                paramTypeBuilder.Add(item.Tag, item.Type);
                valueParentBuilder.Add(item.Tag, tag);
            }

            newGraph.blockParamTypes = paramTypeBuilder.ToImmutable();
            newGraph.valueParents = valueParentBuilder.ToImmutable();
            newGraph.blocks = newGraph.blocks.SetItem(tag, newData);

            return new BasicBlock(newGraph, tag, newData);
        }

        // internal BasicBlock UpdateBasicBlockData(BasicBlockTag tag, BasicBlockData data)
        // {
        //     AssertContainsBasicBlock(tag);
        //     var newGraph = new FlowGraph(this);
        //     newGraph.blocks = newGraph.blocks.SetItem(tag, data);

        //     var paramTypeBuilder = newGraph.blockParamTypes.ToBuilder();
        //     var valueParentBuilder = newGraph.valueParents.ToBuilder();

        //     // Remove the basic block from the value parent and parameter type
        //     // dictionaries.
        //     RemoveBasicBlockImpl(tag, valueParentBuilder, paramTypeBuilder);

        //     // Add the new basic block to the value parent and parameter type
        //     // dictionaries.
        //     var newParams = data.Parameters;
        //     int newParamCount = newParams.Count;
        //     for (int i = 0; i < newParamCount; i++)
        //     {
        //         var item = newParams[i];

        //         ContractHelpers.Assert(
        //             !valueParentBuilder.ContainsKey(item.Tag),
        //             "Value tag '" + item.Tag.Name + "' cannot appear twice in the same control-flow graph.");

        //         paramTypeBuilder.Add(item.Tag, item.Type);
        //         valueParentBuilder.Add(item.Tag, tag);
        //     }

        //     var newInstrs = data.Instructions;
        //     int newInstrCount = newInstrs.Count;
        //     for (int i = 0; i < newInstrCount; i++)
        //     {
        //         var item = newInstrs[i];

        //         ContractHelpers.Assert(
        //             !valueParentBuilder.ContainsKey(item),
        //             "Value tag '" + item.Name + "' cannot appear twice in the same control-flow graph.");

        //         valueParentBuilder.Add(item, tag);
        //     }

        //     newGraph.blockParamTypes = paramTypeBuilder.ToImmutable();
        //     newGraph.valueParents = valueParentBuilder.ToImmutable();

        //     return new BasicBlock(newGraph, tag, data);
        // }
    }
}