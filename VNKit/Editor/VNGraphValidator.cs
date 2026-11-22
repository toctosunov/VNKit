using System.Collections.Generic;
using VNKit;

namespace VNKitEditor
{
    public enum VNValidationSeverity { Warning, Error }

    public struct VNValidationIssue
    {
        public string message;
        public string nodeId;   // the frame to jump to from the "Select frame" button, if any
        public VNValidationSeverity severity;
    }

    /// <summary>
    /// Static checks over a VNStory graph: dangling links (choice/branch/next pointing at a frame
    /// that no longer exists), a missing start frame, and frames the story can never reach.
    /// Pure read-only inspection - run from the editor window, never from the player.
    /// </summary>
    public static class VNGraphValidator
    {
        public static List<VNValidationIssue> Validate(VNStory story)
        {
            var issues = new List<VNValidationIssue>();
            if (story == null || story.nodes.Count == 0) return issues;

            if (string.IsNullOrEmpty(story.startNodeId) || story.GetNode(story.startNodeId) == null)
            {
                issues.Add(new VNValidationIssue
                {
                    message = "No valid start frame is set; the first frame in the list will be used at runtime.",
                    severity = VNValidationSeverity.Warning
                });
            }

            var ids = new HashSet<string>();
            foreach (var n in story.nodes) ids.Add(n.id);

            foreach (var n in story.nodes)
            {
                string label = string.IsNullOrEmpty(n.title) ? "(untitled)" : n.title;

                if (n.choices.Count > 0)
                {
                    for (int i = 0; i < n.choices.Count; i++)
                    {
                        var target = n.choices[i].targetId;
                        if (string.IsNullOrEmpty(target) || !ids.Contains(target))
                            issues.Add(Error($"\"{label}\": choice {i + 1} (\"{n.choices[i].text}\") doesn't link to a frame.", n.id));
                    }
                }
                else if (n.branches.Count > 0)
                {
                    for (int i = 0; i < n.branches.Count; i++)
                    {
                        var target = n.branches[i].targetId;
                        if (string.IsNullOrEmpty(target) || !ids.Contains(target))
                            issues.Add(Error($"\"{label}\": branch {i + 1} doesn't link to a frame.", n.id));
                    }
                    if (!string.IsNullOrEmpty(n.nextId) && !ids.Contains(n.nextId))
                        issues.Add(Error($"\"{label}\": fallback Next link doesn't point to a frame.", n.id));
                }
                else if (!string.IsNullOrEmpty(n.nextId) && !ids.Contains(n.nextId))
                {
                    issues.Add(Error($"\"{label}\": Next link doesn't point to a frame.", n.id));
                }
            }

            var start = story.GetNode(story.startNodeId) ?? story.nodes[0];
            var reachable = ReachableFrom(story, start);
            foreach (var n in story.nodes)
            {
                if (reachable.Contains(n.id)) continue;
                string label = string.IsNullOrEmpty(n.title) ? "(untitled)" : n.title;
                issues.Add(Warning($"\"{label}\" is never reached from the start frame.", n.id));
            }

            return issues;
        }

        static VNValidationIssue Error(string message, string nodeId) =>
            new VNValidationIssue { message = message, nodeId = nodeId, severity = VNValidationSeverity.Error };

        static VNValidationIssue Warning(string message, string nodeId) =>
            new VNValidationIssue { message = message, nodeId = nodeId, severity = VNValidationSeverity.Warning };

        static HashSet<string> ReachableFrom(VNStory story, VNNodeData start)
        {
            var visited = new HashSet<string>();
            if (start == null) return visited;
            var stack = new Stack<string>();
            stack.Push(start.id);

            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (string.IsNullOrEmpty(id) || visited.Contains(id)) continue;
                var n = story.GetNode(id);
                if (n == null) continue;
                visited.Add(id);

                if (n.choices.Count > 0)
                {
                    foreach (var c in n.choices) stack.Push(c.targetId);
                }
                else if (n.branches.Count > 0)
                {
                    foreach (var b in n.branches) stack.Push(b.targetId);
                    stack.Push(n.nextId);
                }
                else
                {
                    stack.Push(n.nextId);
                }
            }
            return visited;
        }
    }
}
