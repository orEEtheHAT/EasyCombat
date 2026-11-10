using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace EasyCombat.Samples
{
    public sealed class AttackGallery : MonoBehaviour
    {
        [Serializable]
        public sealed class Example
        {
            public ScriptableObject Recipe;
            public string Title;
            [TextArea] public string Description;
            public Color Tint;
        }

        public Example[] Examples;
        public DamageEmitter Launcher;
        public CombatBeam Beam;
        public CombatMelee Melee;
        public Transform SwordTip;
        public LineRenderer Trace;
        public Health[] Targets;
        public CombatStatDefinition SpeedStat;
        public CombatJournal Journal;
        public UIDocument Controls;
        public StyleSheet Styles;

        private StatusController[] statuses;
        private Vector3[] home;
        private float[] phases;
        private float swingTime, traceUntil, nextReadout;
        private Vector3 swingDirection;
        private MeleeDefinition currentSwing;
        private Button[] selectors;
        private Label description, readout;
        private int selected;

        private void Start()
        {
            home = new Vector3[Targets.Length];
            phases = new float[Targets.Length];
            statuses = new StatusController[Targets.Length];
            for (int i = 0; i < Targets.Length; i++)
            {
                home[i] = Targets[i].transform.position;
                statuses[i] = Targets[i].GetComponent<StatusController>();
            }
            Melee.AutomaticAdvance = false;
            Melee.SetAnchors(SwordTip);
            Launcher.Fired += ShowShot;
            Beam.Pulsed += ShowShot;
            BuildControls();
            SelectExample(0);
        }

        public void SelectExample(int index)
        {
            if (index < 0 || index >= Examples.Length) throw new ArgumentOutOfRangeException(nameof(index));
            selected = index;
            for (int i = 0; i < selectors.Length; i++)
                selectors[i].EnableInClassList("selected", i == index);
            description.text = Examples[index].Description;
            ResetTargets();
        }

        public void ResetTargets()
        {
            Melee.Stop();
            Beam.Stop();
            Launcher.Pool.ClearActive();
            Launcher.AreaPool.ClearActive();
            Launcher.ResetCooldown();
            Trace.enabled = false;
            for (int i = 0; i < Targets.Length; i++)
            {
                Targets[i].ResetToFull();
                phases[i] = 0;
                Targets[i].transform.position = home[i];
            }
            Physics.SyncTransforms();
            Journal.Clear();
            RefreshReadout();
        }

        public bool Fire()
        {
            var recipe = Examples[selected].Recipe;
            var muzzle = Launcher.transform;
            var direction = (Targets[0].transform.position - muzzle.position).normalized;
            muzzle.rotation = Quaternion.LookRotation(direction);
            Physics.SyncTransforms();
            if (recipe is MeleeDefinition sword)
            {
                if (Melee.IsRunning) return false;
                currentSwing = sword;
                swingDirection = direction;
                swingTime = 0;
                PoseBlade(0);
                return Melee.Begin(sword);
            }
            if (recipe is BeamDefinition beam) return Beam.Begin(beam);
            Launcher.Attack = (AttackDefinition)recipe;
            bool fired = Launcher.TryFire(muzzle.position, direction);
            if (fired && Launcher.Attack.Delivery == DamageDelivery.Area)
                ShowArea(muzzle.position, direction, Launcher.Attack.Area);
            return fired;
        }

        private void Update()
        {
            if (home == null) return;
            float delta = Time.deltaTime;
            for (int i = 0; i < Targets.Length; i++)
            {
                if (!Targets[i].IsAlive) continue;
                phases[i] += delta * statuses[i].GetStatMultiplier(SpeedStat);
                Targets[i].transform.position = home[i] + Vector3.right * (.20f * Mathf.Sin(phases[i] * 2));
            }
            if (Melee.IsRunning && delta > 0)
            {
                swingTime += delta;
                PoseBlade(Mathf.Clamp01(swingTime / currentSwing.Duration));
                Melee.Advance(delta);
                DrawLine(Launcher.transform.position, SwordTip.position, .11f, .05f);
            }
            if (Time.time >= traceUntil) Trace.enabled = false;
            if (Time.unscaledTime >= nextReadout)
            {
                nextReadout = Time.unscaledTime + .12f;
                RefreshReadout();
            }
        }

        private void PoseBlade(float t)
        {
            var direction = Quaternion.AngleAxis(Mathf.Lerp(-55, 55, t), Vector3.up) * swingDirection;
            SwordTip.position = Launcher.transform.position + direction * 3f;
        }

        private void ShowShot(ShotInfo shot)
        {
            if (shot.Delivery == DamageDelivery.Hitscan)
                DrawLine(shot.Origin, shot.End, Mathf.Max(.055f, shot.Radius * 2), .14f);
        }

        private void DrawLine(Vector3 a, Vector3 b, float width, float lifetime)
        {
            ConfigureTrace(2, width, lifetime);
            Trace.SetPosition(0, a);
            Trace.SetPosition(1, b);
        }

        private void ShowArea(Vector3 origin, Vector3 forward, AreaDefinition area)
        {
            const int steps = 36;
            bool cone = area.Shape == AreaShape.Cone;
            float angle = cone ? area.ConeAngle : 360f;
            origin.y = .08f;
            ConfigureTrace(steps + (cone ? 3 : 1), .06f, .45f);
            if (cone) Trace.SetPosition(0, origin);
            for (int i = 0; i <= steps; i++)
            {
                var radial = Quaternion.AngleAxis(angle * (i / (float)steps - .5f), Vector3.up) * forward;
                Trace.SetPosition(i + (cone ? 1 : 0), origin + radial * area.Radius);
            }
            if (cone) Trace.SetPosition(steps + 2, origin);
        }

        private void ConfigureTrace(int points, float width, float lifetime)
        {
            Trace.positionCount = points;
            Trace.startWidth = Trace.endWidth = width;
            Trace.startColor = Trace.endColor = Examples[selected].Tint;
            Trace.enabled = true;
            traceUntil = Time.time + lifetime;
        }

        private void BuildControls()
        {
            var root = Controls.rootVisualElement;
            root.styleSheets.Add(Styles);
            var panel = new ScrollView(); panel.AddToClassList("gallery"); root.Add(panel);
            var title = new Label("Attack Gallery"); title.AddToClassList("title"); panel.Add(title);
            var choices = new VisualElement(); choices.AddToClassList("choices"); panel.Add(choices);
            selectors = new Button[Examples.Length];
            for (int i = 0; i < Examples.Length; i++)
            {
                int index = i;
                var button = new Button(() => SelectExample(index)) { text = Examples[i].Title };
                button.AddToClassList("choice"); choices.Add(button); selectors[i] = button;
            }
            description = new Label(); description.AddToClassList("description"); panel.Add(description);
            var actions = new VisualElement(); actions.AddToClassList("actions"); panel.Add(actions);
            var fire = new Button(() => Fire()) { name = "fire", text = "Attack" };
            fire.AddToClassList("fire"); actions.Add(fire);
            actions.Add(new Button(ResetTargets) { text = "Reset targets" });
            readout = new Label(); readout.AddToClassList("readout"); panel.Add(readout);
        }

        private void RefreshReadout()
        {
            var text = new StringBuilder();
            for (int i = 0; i < Targets.Length; i++)
            {
                text.Append(Targets[i].name).Append("   ").Append(Targets[i].Current.ToString("0.#"))
                    .Append(" / ").Append(Targets[i].Maximum.ToString("0")).Append(" HP");
                for (int s = 0; s < statuses[i].Count; s++)
                {
                    var status = statuses[i].GetStatus(s);
                    text.Append("   ").Append(status.Definition.Label).Append(" ×").Append(status.Stacks);
                }
                text.AppendLine();
            }
            if (Journal.Count > 0)
            {
                var last = Journal.GetNewest(0);
                text.Append("\nLast hit   ").Append(last.Target.Name).Append("   −")
                    .Append(last.Result.AppliedAmount.ToString("0.#")).Append(" HP");
            }
            readout.text = text.ToString();
        }

        private void OnDestroy()
        {
            if (Launcher != null) Launcher.Fired -= ShowShot;
            if (Beam != null) Beam.Pulsed -= ShowShot;
        }
    }
}
