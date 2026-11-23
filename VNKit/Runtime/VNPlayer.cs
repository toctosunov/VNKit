using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Plays a VNStory. Create the UI with Tools > Visual Novel > Create Player In Scene.
    /// This class is the orchestrator: it drives the flow (show frame -> type text -> wait for
    /// input/choice -> go to the next frame) and delegates the actual work to small presenters
    /// (<see cref="VNVisualLayerController"/>, <see cref="VNTextPresenter"/>, <see cref="VNChoicePresenter"/>,
    /// <see cref="VNAudioController"/>) plus the <see cref="VNVariables"/> store for conditions/effects.
    /// </summary>
    public class VNPlayer : MonoBehaviour
    {
        [Header("Story")]
        public VNStory story;
        public bool playOnStart = true;

        [Header("Scene layers")]
        public Image backgroundImage;
        public Image singleImage;

        [Header("Characters (leave unused slots empty)")]
        public Image leftCharacterImage;
        public Image centerCharacterImage;
        public Image rightCharacterImage;

        [Header("Text")]
        public CanvasGroup textPanel;
        public TMP_Text speakerLabel;
        public TMP_Text bodyLabel;

        [Header("Choices")]
        public RectTransform choicesContainer;
        public Button choiceButtonTemplate;

        [Header("Input")]
        public Button advanceButton;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Header("Transitions")]
        [Tooltip("Full-screen overlay used by Start frames (fades out to reveal the story) and End frames (fades in to hide it). Optional.")]
        public Image curtain;

        [Tooltip("On: after the question text is read, a click replaces it with the answers. Off: answers replace the text right away.")]
        public bool choicesAfterClick = true;

        public UnityEvent onStoryEnd;

        /// <summary>The story's variables for the current playthrough. Also what conditions/effects read and write.</summary>
        public VNVariables Variables { get; } = new VNVariables();

        VNVisualLayerController visuals;
        VNTextPresenter textPresenter;
        VNChoicePresenter choicePresenter;
        VNAudioController audio;
        VNCurtainController curtainController;

        List<VNNodeData> order = new List<VNNodeData>();
        readonly Dictionary<string, int> orderIndex = new Dictionary<string, int>();
        VNNodeData current;
        List<VNChoice> currentEligibleChoices;
        VNClip activeBg;
        bool waitingForChoices;

        void Awake()
        {
            visuals = new VNVisualLayerController(backgroundImage, singleImage, leftCharacterImage, centerCharacterImage, rightCharacterImage);
            textPresenter = new VNTextPresenter(textPanel, speakerLabel, bodyLabel);
            choicePresenter = new VNChoicePresenter(choicesContainer, choiceButtonTemplate);
            audio = new VNAudioController(musicSource, sfxSource);
            curtainController = new VNCurtainController(curtain);

            choicePresenter.AlignToBody(textPresenter.PanelTransform, textPresenter.BodyRect);
            if (advanceButton) advanceButton.onClick.AddListener(Advance);

            if (leftCharacterImage == null && centerCharacterImage == null && rightCharacterImage == null)
                Debug.LogWarning("VNPlayer: no character slot image is assigned, so characters will not be shown.", this);
        }

        void Start()
        {
            if (playOnStart && story != null) Play(story);
        }

        // ---------------------------------------------------------------- flow

        public void Play(VNStory s)
        {
            if (s == null)
            {
                Debug.LogWarning("VNPlayer: Play() called with a null story.", this);
                return;
            }
            story = s;
            order = story.GetOrderedNodes();
            RebuildOrderIndex();
            Variables.Init(story);
            audio.Reset();
            activeBg = null;

            var start = story.GetStartNode();
            if (start == null) { End(); return; }
            Show(start);
        }

        public void Restart()
        {
            if (story != null) Play(story);
        }

        public void Advance()
        {
            if (current == null || story == null) return;
            if (current.kind != VNNodeKind.Story) return;   // Start/End frames drive themselves, no player input
            if (textPresenter.Typing) { textPresenter.SkipTyping(); return; }
            if (waitingForChoices) { waitingForChoices = false; ShowChoices(); return; }
            if (currentEligibleChoices != null && currentEligibleChoices.Count > 0) return;   // must pick a choice
            GoTo(ResolveNext(current));
        }

        string ResolveNext(VNNodeData n)
        {
            if (n.choices.Count == 0 && n.branches != null)
            {
                for (int i = 0; i < n.branches.Count; i++)
                    if (VNCondition.EvaluateAll(n.branches[i].conditions, Variables))
                        return n.branches[i].targetId;
            }
            return n.nextId;
        }

        void GoTo(string id)
        {
            var n = story != null ? story.GetNode(id) : null;
            if (n == null) End();
            else Show(n);
        }

        void End()
        {
            current = null;
            currentEligibleChoices = null;
            choicePresenter.Clear();
            onStoryEnd?.Invoke();
        }

        void Show(VNNodeData n)
        {
            current = n;
            currentEligibleChoices = null;
            waitingForChoices = false;
            choicePresenter.Clear();

            if (n.kind == VNNodeKind.Start) { ShowStart(n); return; }
            if (n.kind == VNNodeKind.End) { ShowEnd(n); return; }

            VNVariableOp.ApplyAll(n.onEnterEffects, Variables);

            int idx = IndexOfId(n.id);
            activeBg = FindClip(VNTrack.Background, idx);
            audio.ApplyMusic(FindClip(VNTrack.Music, idx));
            ApplyVisuals(n);

            if (n.sfx != null) audio.PlaySfx(n.sfx, n.sfxVolume);
            textPresenter.Show(story, n, OnTextDone);
        }

        /// <summary>No content of its own: just a curtain fade-in, then automatically moves to nextId.</summary>
        void ShowStart(VNNodeData n)
        {
            curtainController.FadeIn(n.startFade, () => GoTo(n.nextId));
        }

        /// <summary>
        /// Terminal: a curtain fade-out, then either a scene load (if endSceneToLoad parses to a
        /// non-negative build index) or End() (story stays on the now-opaque curtain).
        /// </summary>
        void ShowEnd(VNNodeData n)
        {
            curtainController.FadeOut(n.endFade, () =>
            {
                if (int.TryParse(n.endSceneToLoad, out int sceneIndex) && sceneIndex >= 0)
                    UnityEngine.SceneManagement.SceneManager.LoadScene(sceneIndex);
                else
                    End();
            });
        }

        // ---------------------------------------------------------------- timeline clips

        void RebuildOrderIndex()
        {
            orderIndex.Clear();
            for (int i = 0; i < order.Count; i++) orderIndex[order[i].id] = i;
        }

        int IndexOfId(string id) =>
            !string.IsNullOrEmpty(id) && orderIndex.TryGetValue(id, out int i) ? i : -1;

        VNClip FindClip(VNTrack track, int nodeIndex)
        {
            VNClip best = null;
            int bestStart = -1;
            foreach (var c in story.clips)
            {
                if (c.track != track) continue;
                int s = IndexOfId(c.startNodeId);
                int e = IndexOfId(c.endNodeId);
                if (s < 0 || e < 0) continue;
                if (s > e) { int t = s; s = e; e = t; }
                if (nodeIndex >= s && nodeIndex <= e && s >= bestStart)
                {
                    best = c;
                    bestStart = s;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- visuals

        void ApplyVisuals(VNNodeData n)
        {
            if (n.layout == VNLayout.Scene)
            {
                Sprite bg = n.background;
                VNAnim bgAnim = n.backgroundAnim;
                if (bg == null && activeBg != null && activeBg.sprite != null)
                {
                    bg = activeBg.sprite;
                    bgAnim = new VNAnim { type = VNAnimType.Fade, duration = activeBg.fadeIn };
                }
                visuals.ApplyScene(bg, bgAnim, n.characters);
            }
            else
            {
                visuals.ApplySingleImage(n.image, n.imageAnim);
            }
        }

        // ---------------------------------------------------------------- text & choices

        void OnTextDone()
        {
            if (current == null) return;
            currentEligibleChoices = FilterChoices(current.choices);
            if (currentEligibleChoices.Count == 0) return;   // nothing to pick: Advance() will move on via branches/nextId

            if (choicesAfterClick && !string.IsNullOrWhiteSpace(current.text)) waitingForChoices = true;
            else ShowChoices();
        }

        List<VNChoice> FilterChoices(List<VNChoice> choices)
        {
            var result = new List<VNChoice>(choices.Count);
            for (int i = 0; i < choices.Count; i++)
                if (VNCondition.EvaluateAll(choices[i].conditions, Variables)) result.Add(choices[i]);
            return result;
        }

        void ShowChoices()
        {
            textPresenter.SetBodyVisible(false);
            choicePresenter.Show(currentEligibleChoices, textPresenter.ActiveStyle, textPresenter.FallbackBodyFont, Choose);
        }

        void Choose(VNChoice choice)
        {
            VNVariableOp.ApplyAll(choice.effects, Variables);
            choicePresenter.Clear();
            currentEligibleChoices = null;
            GoTo(choice.targetId);
        }

        // ---------------------------------------------------------------- save / load

        /// <summary>A snapshot of where the player is and what the story's variables are, ready for VNSaveSystem.</summary>
        public VNSaveData CaptureState()
        {
            var data = new VNSaveData
            {
                storyName = story != null ? story.name : null,
                currentNodeId = current != null ? current.id : null,
            };
            Variables.Snapshot(out var names, out var values);
            data.varNames = names;
            data.varValues = values;
            return data;
        }

        /// <summary>Resumes a previously captured state on the given story (not necessarily the one currently assigned).</summary>
        public void ApplyState(VNStory s, VNSaveData data)
        {
            if (s == null || data == null)
            {
                Debug.LogWarning("VNPlayer: ApplyState() called with a null story or save data.", this);
                return;
            }
            story = s;
            order = story.GetOrderedNodes();
            RebuildOrderIndex();
            Variables.Init(story);
            Variables.Restore(data.varNames, data.varValues);
            audio.Reset();
            activeBg = null;

            var node = story.GetNode(data.currentNodeId);
            if (node == null && story.nodes.Count > 0) node = story.nodes[0];
            if (node == null) { End(); return; }
            Show(node);
        }
    }
}
