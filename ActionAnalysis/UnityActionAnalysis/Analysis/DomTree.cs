using System;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.CSharp;

namespace UnityActionAnalysis
{

    public class DomTree
    {
        private Dictionary<CFGNode, CFGNode> tree;

        public DomTree()
        {
            tree = new Dictionary<CFGNode, CFGNode>();
        }

        public void SetRoot(CFGNode node)
        {
            tree.Add(node, null);
        }

        public void AddNode(CFGNode node, CFGNode parent)
        {
            tree.Add(node, parent);
        }

        public CFGNode GetParent(CFGNode node)
        {
            return tree[node];
        }

        public bool DoesDominate(CFGNode m, CFGNode n)
        {
            while (n != null)
            {
                if (m.Equals(n))
                {
                    return true;
                }
                n = GetParent(n);
            }
            return false;
        }

        private List<CFGNode> NodeAncestorPath(CFGNode n)
        {
            List<CFGNode> res = new List<CFGNode>();
            while (n != null)
            {
                res.Add(n);
                n = GetParent(n);
            }
            return res;
        }

        public CFGNode LowestCommonAncestor(CFGNode m, CFGNode n)
        {
            List<CFGNode> mPath = NodeAncestorPath(m);
            List<CFGNode> nPath = NodeAncestorPath(n);
            int i = mPath.Count - 1;
            int j = nPath.Count - 1;
            Debug.Assert(mPath[i].Equals(nPath[j]));
            CFGNode result = mPath[i];
            --i;
            --j;
            while (i >= 0 && j >= 0)
            {
                if (!mPath[i].Equals(nPath[j]))
                {
                    break;
                }
                result = mPath[i];
                --i;
                --j;
            }
            return result;
        }

        public override bool Equals(object obj)
        {
            if (obj is DomTree tree)
            {
                return this.tree.ToHashSet().SetEquals(tree.tree.ToHashSet());
            } else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int result = 1;
            foreach (var entry in tree)
            {
                result = 31*result + entry.Key.GetHashCode();
                result = 31*result + entry.Value.GetHashCode();
            }
            return result;
        }

        private static Dictionary<CFGNode, ISet<CFGNode>> computeDominators(CFG cfg, CFGNode head)
        {
            Dictionary<CFGNode, ISet<CFGNode>> dom = new Dictionary<CFGNode, ISet<CFGNode>>();
            foreach (CFGNode node in cfg.Nodes)
            {
                ISet<CFGNode> nodeDom = new HashSet<CFGNode>();
                if (node.Equals(head))
                {
                    nodeDom.Add(head);
                } else
                {
                    foreach (CFGNode n in cfg.Nodes)
                    {
                        nodeDom.Add(n);
                    }
                }
                dom.Add(node, nodeDom);
            }
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (CFGNode node in cfg.Nodes)
                {
                    if (node.Equals(head))
                    {
                        continue;
                    }
                    List<ISet<CFGNode>> predNodeDoms = new List<ISet<CFGNode>>();
                    foreach (CFGNode pred in cfg.Predecessors(node))
                    {
                        predNodeDoms.Add(dom[pred]);
                    }

                    ISet<CFGNode> newNodeDom = CFGNode.IntersectSets(predNodeDoms);
                    newNodeDom.Add(node);
                    ISet<CFGNode> oldNodeDom = dom[node];
                    if (!newNodeDom.SetEquals(oldNodeDom))
                    {
                        changed = true;
                        dom[node] = newNodeDom;
                    }
                }
            }
            return dom;
        }

        private static CFGNode FindIdom(CFGNode n, Dictionary<CFGNode, ISet<CFGNode>> dom)
        {
            ISet<CFGNode> domN = dom[n];
            foreach (CFGNode m in domN)
            {
                if (m.Equals(n))
                {
                    continue;
                }
                ISet<CFGNode> domM = dom[m];
                bool isIdom = true;
                foreach (CFGNode d in domN)
                {
                    if (d.Equals(n))
                    {
                        continue;
                    }
                    if (!domM.Contains(d))
                    {
                        isIdom = false;
                        break;
                    }
                }
                if (isIdom)
                {
                    return m;
                }
            }
            throw new Exception("every node should have a unique idom");
        }

        public static DomTree MakeDomTree(CFG cfg)
        {
            CFGNode head = cfg.FindHead();
            Dictionary<CFGNode, ISet<CFGNode>> dom = computeDominators(cfg, head);
            DomTree t = new DomTree();
            t.SetRoot(head);
            foreach (CFGNode n in cfg.Nodes)
            {
                if (n.Equals(head))
                {
                    continue;
                }
                CFGNode m = FindIdom(n, dom);
                t.AddNode(n, m);
            }
            return t;
        }

        public String ToDotty() {
            StringBuilder sb = new StringBuilder();
            sb.Append("digraph domtree {\n");
            foreach (CFGNode n in tree.Keys) {
                CFGNode parent = tree[n];
                if (parent != null) {
                    sb.Append("\t\"");
                    sb.Append(parent.GetLabel().Replace("\"", "\\\""));
                    sb.Append("\"->\"");
                    sb.Append(n.GetLabel().Replace("\"", "\\\""));
                    sb.Append("\"\n");
                }
            }
            sb.Append("}");
            return sb.ToString();
        }
    }

}