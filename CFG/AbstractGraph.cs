using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.CFG
{
    /// <summary>
    /// A minimalist graph for encoding control flow graphs. Intended to be used for interval analysis
    /// </summary>
    public class AbstractGraph
    {
        public Node BeginNode = null;
        public List<Node> Nodes = new List<Node>();
        public Dictionary<Node, HashSet<Node>> Intervals = null;

        public Dictionary<Node, Node> LoopHeads = new Dictionary<Node, Node>();
        public Dictionary<Node, List<Node>> LoopLatches = new Dictionary<Node, List<Node>>();
        public Dictionary<Node, Node> LoopFollows = new Dictionary<Node, Node>();
        public Dictionary<Node, LoopType> LoopTypes = new Dictionary<Node, LoopType>();

        public class Node : IComparable<Node>
        {
            public List<Node> Successors = new List<Node>();
            public List<Node> Predecessors = new List<Node>();

            /// <summary>
            /// Child in the successor graph. Only set if this is an interval graph head
            /// </summary>
            public Node IntervalGraphChild = null;

            /// <summary>
            /// The interval head in the parent graph that collapses into this node
            /// </summary>
            public Node IntervalGraphParent = null;

            /// <summary>
            /// Pointer to the interval set this node belongs to
            /// </summary>
            public HashSet<Node> Interval = null;

            /// <summary>
            /// The basic block that this node ultimately maps to
            /// </summary>
            public BasicBlock OriginalBlock = null;

            /// <summary>
            /// Node is marked as being part of a loop
            /// </summary>
            public bool InLoop = false;

            public bool IsHead = false;

            /// <summary>
            /// Node is the terminal node (where returns jump to)
            /// </summary>
            public bool IsTerminal = false;

            /// <summary>
            /// The node that's the predecessor to the loop follow
            /// </summary>
            //public Node FollowLeader = null;

            /// <summary>
            /// Number in this graph based on the reverse postorder traversal number
            /// </summary>
            public int ReversePostorderNumber = 0;

            public int CompareTo(Node other)
            {
                if (other.ReversePostorderNumber > ReversePostorderNumber)
                    return -1;
                else if (other.ReversePostorderNumber == ReversePostorderNumber)
                    return 0;
                else
                    return 1;
            }
        }

        public List<Node> PostorderTraversal(bool reverse)
        {
            var ret = new List<Node>();
            var visited = new HashSet<Node>();

            void Visit(Node b)
            {
                visited.Add(b);
                foreach (var succ in b.Successors.OrderByDescending(n => n.OriginalBlock.BlockID))
                {
                    if (!visited.Contains(succ))
                    {
                        Visit(succ);
                    }
                }
                ret.Add(b);
            }

            Visit(BeginNode);

            if (reverse)
            {
                ret.Reverse();
            }
            return ret;
        }

        public void LabelReversePostorderNumbers()
        {
            var postorder = PostorderTraversal(true);
            for (int i = 0; i < postorder.Count(); i++)
            {
                postorder[i].ReversePostorderNumber = i;
            }
        }

        // Thanks @thefifthmatt :forestcat:
        public void CalculateIntervals()
        {
            Intervals = new Dictionary<Node, HashSet<Node>>();
            var headers = new HashSet<Node> { BeginNode };
            while (headers.Count() > 0)
            {
                var h = headers.First();
                headers.Remove(h);
                var interval = new HashSet<Node> { h };
                Intervals.Add(h, interval);
                h.Interval = interval;
                int lastCount = 0;
                while (lastCount != interval.Count)
                {
                    lastCount = interval.Count;
                    foreach (var start in interval.ToList())
                    {
                        foreach (var cand in start.Successors)
                        {
                            if (cand.Predecessors.All(n => interval.Contains(n)) && !Intervals.ContainsKey(cand))
                            {
                                interval.Add(cand);
                                cand.Interval = interval;
                            }
                        }
                    }
                }
                foreach (var cand in interval)
                {
                    headers.UnionWith(cand.Successors.Except(interval).Except(Intervals.Keys));
                }
            }
        }

        /// <summary>
        /// Get a collapsed version of the graph where interval heads become nodes
        /// </summary>
        /// <returns>A new collapsed graph</returns>
        public AbstractGraph GetIntervalSubgraph()
        {
            if (Intervals == null)
            {
                CalculateIntervals();
            }
            if (Intervals.Count() == Nodes.Count() || Intervals.Values.All(i => i.Count == 1))
            {
                return null;
            }

            AbstractGraph cfg = new AbstractGraph();
            foreach (var n in Intervals.Keys)
            {
                var node = new Node();
                n.IntervalGraphChild = node;
                node.IntervalGraphParent = n;
                node.OriginalBlock = n.OriginalBlock;
                cfg.Nodes.Add(node);
            }

            var header = Intervals.SelectMany(e => e.Value.Select(i => (i, e.Key))).ToDictionary(i => i.i, i => i.Key);
            foreach (var entry in Nodes)
            {
                if (!header.ContainsKey(entry))
                {
                    continue;
                }
                var h1 = header[entry];
                var hnode = h1.IntervalGraphChild;
                //hnode.Successors.UnionWith(entry.Successors.Select(n => header[n]).Where(h => h != h1).Select(n => n.IntervalGraphChild));
                var nodestoadd = entry.Successors.Select(n => header[n]).Where(h => h != h1).Select(n => n.IntervalGraphChild);
                foreach (var n in nodestoadd)
                {
                    if (!hnode.Successors.Contains(n))
                    {
                        hnode.Successors.Add(n);
                    }
                }
            }
            foreach (var entry in cfg.Nodes)
            {
                foreach (var succ in entry.Successors)
                {
                    succ.Predecessors.Add(entry);
                }
            }
            cfg.BeginNode = cfg.Nodes.First(x => x.Predecessors.Count() == 0);
            cfg.LabelReversePostorderNumbers();
            cfg.CalculateIntervals();
            return cfg;
        }
    }
}
