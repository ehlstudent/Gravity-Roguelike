using System.Linq;
using GravityRoguelike.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Tests
{
    public class VoidKnightTests
    {
        private const string Animations = "Assets/Animations/Player/VoidKnight/";
        private const string Sprites = "Assets/Sprites/Player/VoidKnight/";

        [TestCase("Idle", "Void Knight-Idlet.png", 2)]
        [TestCase("Run", "Void Knight-Run.png", 8)]
        [TestCase("Attack A", "Void Knight-Attack-A-withoutSword.png", 8)]
        [TestCase("Slash", "Void Knight_Slash_Body_Animation.png", 5)]
        [TestCase("Jump Up", "Void Knight-Sheet-Jump.png", 4)]
        [TestCase("Fall", "Void Knight-Sheet-Jump.png", 1)]
        [TestCase("Land", "Void Knight-Sheet-Jump.png", 4)]
        public void BodyUsesOnlyTheSelectedSwordlessSheet(string animation, string sheet, int count)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + animation + ".anim");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            Assert.That(binding.path, Is.Empty, "The body animator must not animate the weapon or player root.");
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.That(frames.Select(f => f.value).Distinct().Count(), Is.EqualTo(count));
            Assert.That(frames.All(f => AssetDatabase.GetAssetPath(f.value) == Sprites + sheet), Is.True);
            foreach (Sprite sprite in frames.Select(f => f.value).Distinct())
            {
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(96, 96)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
                Assert.That(Vector2.Distance(sprite.pivot, new Vector2(44, 32)), Is.LessThan(0.001f));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(Sprites + sheet);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(clip.isLooping, Is.EqualTo(animation == "Idle" || animation == "Run" || animation == "Fall"));
        }

        [Test]
        public void StabHasSeparateInputAndKeepsTheAlternatingSwingStates()
        {
            var inputs = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            var stab = inputs.FindAction("Player/Attack A-stab", true);
            var swing = inputs.FindAction("Player/Attack", true);
            Assert.That(stab.bindings.Select(b => b.path), Does.Contain("<Mouse>/rightButton"));
            Assert.That(stab.bindings.Select(b => b.path), Does.Contain("<Keyboard>/f"));
            Assert.That(stab.bindings.Select(b => b.path).Intersect(swing.bindings.Select(b => b.path)), Is.Empty);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "Sword.controller");
            var states = controller.layers[0].stateMachine.states;
            foreach (string name in new[] { "Idle", "SwingUp", "SwingDown" })
                Assert.That(states.Single(s => s.state.name == name).state.motion,
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<AnimationClip>(name == "Idle"
                        ? Animations + "Sword Idle.anim" : "Assets/Animations/Combat/" + name + ".anim")));
            Assert.That(controller.parameters.Any(p => p.name == "Attack A-stab" && p.type == AnimatorControllerParameterType.Trigger), Is.True);
        }

        [TestCase("Attack A", "StabSpeed", "Stabbing")]
        [TestCase("Slash", "SlashSpeed", "Slashing")]
        public void BodyAttackReturnsToTheCurrentMovementStateWhenSwordFinishes(string state, string speed, string active)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "Body.controller");
            var states = controller.layers[0].stateMachine.states;
            var attack = states.Single(s => s.state.name == state).state;
            Assert.That(attack.speedParameter, Is.EqualTo(speed));
            Assert.That(attack.speedParameterActive, Is.True);
            foreach (var t in attack.transitions)
            {
                Assert.That(t.hasExitTime, Is.False, "Sword completion/cancellation controls the return.");
                Assert.That(t.conditions.Any(c => c.parameter == active && c.mode == AnimatorConditionMode.IfNot), Is.True);
                string destination = t.destinationState.name;
                bool grounded = destination == "Idle" || destination == "Run";
                Assert.That(t.conditions.Any(c => c.parameter == "IsGrounded" &&
                    c.mode == (grounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot)), Is.True);
                string condition = grounded ? "Running" : "IsRising";
                bool positive = destination == "Run" || destination == "Jump Up";
                Assert.That(t.conditions.Any(c => c.parameter == condition &&
                    c.mode == (positive ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot)), Is.True);
                if (destination == "Jump Up") Assert.That(t.offset, Is.EqualTo(1f), "Resume the held pose, not the jump intro.");
            }
            Assert.That(attack.transitions.Select(t => t.destinationState.name),
                Is.EquivalentTo(new[] { "Idle", "Run", "Jump Up", "Fall" }));
        }

        [TestCase("Slash", "Slashing", "Jump Up", false, true, "Jump Up")]
        [TestCase("Slash", "Slashing", "Fall", false, false, "Fall")]
        [TestCase("Slash", "Slashing", "Land", true, false, "Idle")]
        [TestCase("Attack A", "Stabbing", "Jump Up", false, true, "Jump Up")]
        [TestCase("Attack A", "Stabbing", "Fall", false, false, "Fall")]
        [TestCase("Attack A", "Stabbing", "Land", true, false, "Idle")]
        [TestCase("Slash", "Slashing", "Jump Up", true, false, "Idle")]
        [TestCase("Attack A", "Stabbing", "Jump Up", false, false, "Fall")]
        public void AirAttacksInterruptAndReturnUsingCurrentPhysics(string attack, string active,
            string start, bool groundedAfter, bool risingAfter, string expected)
        {
            var root = new GameObject("Air animation test", typeof(SpriteRenderer), typeof(Animator));
            try
            {
                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "Body.controller");
                animator.Rebind();
                animator.SetFloat("StabSpeed", 1f);
                animator.SetFloat("SlashSpeed", 1f);
                animator.SetBool("IsGrounded", start == "Land");
                animator.SetBool("IsRising", start == "Jump Up");
                animator.Play("Base Layer." + start, 0, 0.4f);
                animator.Update(0f);
                animator.SetBool(active, true);
                animator.SetTrigger(attack);
                animator.Update(0.01f);
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(attack), Is.True, "Attack must interrupt without exit time.");
                animator.SetBool("IsGrounded", groundedAfter);
                animator.SetBool("IsRising", risingAfter);
                animator.SetBool(active, false);
                animator.Update(0.01f);
                animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(expected), Is.True);
                if (expected == "Jump Up")
                {
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1f));
                    Assert.That(root.GetComponent<SpriteRenderer>().sprite.name, Does.EndWith("_3"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void StabDamageWindowMatchesExtensionAndRetraction()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + "Attack A-stab.anim");
            Assert.That(clip.length, Is.EqualTo(1).Within(0.001f));
            var events = AnimationUtility.GetAnimationEvents(clip);
            Assert.That(events.Select(e => e.functionName), Is.EqualTo(new[] { "OpenStabHitWindow", "CloseHitWindow", "FinishAttack" }));
            Assert.That(events[0].time, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(events[1].time, Is.EqualTo(0.72f).Within(0.001f));
            var root = new GameObject("Stab geometry test");
            try
            {
                var visual = new GameObject("StabVisual", typeof(SpriteRenderer));
                visual.transform.SetParent(root.transform, false);
                var hit = new GameObject("StabHitPoint");
                hit.transform.SetParent(visual.transform, false);
                clip.SampleAnimation(root, 0.56f);
                Assert.That(visual.transform.localPosition.x, Is.GreaterThan(0.9f));
                Assert.That(hit.transform.localScale.x, Is.EqualTo(62f / 32).Within(0.001f));
                Assert.That(hit.transform.localScale.y, Is.EqualTo(9f / 32).Within(0.001f));
                Assert.That(hit.transform.localPosition.x, Is.EqualTo(31f / 32).Within(0.001f));
                var spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                var extensionFrame = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding)
                    .Last(f => f.time <= 0.56f).value;
                Assert.That(extensionFrame.name, Does.EndWith("_4"));
                clip.SampleAnimation(root, 0.95f);
                Assert.That(visual.transform.localPosition.x, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(hit.transform.localScale.x, Is.EqualTo(34f / 32).Within(0.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PlayerHasIndependentBodyAndWeaponWithSharedStabReferences()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dulli_roomtester.prefab");
            var body = player.transform.Find("Body");
            var sword = player.GetComponentInChildren<Sword>();
            Assert.That(body.GetComponent<PlayerBodyAnimator>(), Is.Not.Null);
            Assert.That(body.GetComponent<Animator>(), Is.Not.EqualTo(sword.GetComponent<Animator>()));
            Assert.That(body.localPosition.y, Is.EqualTo(-0.8f));
            Assert.That(player.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(player.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "Original art reference is preserved.");
            var settings = new SerializedObject(sword);
            foreach (string field in new[] { "stabAction", "stabVisual", "swingVisual", "stabHitPoint" })
                Assert.That(settings.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(player.GetComponentsInChildren<Rigidbody2D>().Length, Is.EqualTo(1));
        }
    }
}
