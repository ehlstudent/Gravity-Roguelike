using System.Linq;
using GravityRoguelike.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Tests
{
    public class SwordCombatTests
    {
        [TestCase(0, 1)]
        [TestCase(90, 1)]
        [TestCase(180, 1)]
        [TestCase(270, 1)]
        [TestCase(0, -1)]
        [TestCase(90, -1)]
        [TestCase(180, -1)]
        [TestCase(270, -1)]
        public void AimWorksAroundTranslatedRotatedAndMirroredPlayer(float gravityAngle, float facing)
        {
            var player = new GameObject("Aim test player");
            try
            {
                player.transform.position = new Vector3(-12, 17, 0);
                player.transform.rotation = Quaternion.Euler(0, 0, gravityAngle);
                player.transform.localScale = new Vector3(facing, 1, 1);
                var pivot = new GameObject("ActiveWeapon", typeof(ActiveWeapon));
                pivot.transform.SetParent(player.transform, false);
                pivot.transform.localPosition = new Vector3(0.2f, 0.3f, 0);
                var animatedSword = new GameObject("Sword");
                animatedSword.transform.SetParent(pivot.transform, false);
                animatedSword.transform.localRotation = Quaternion.Euler(0, 0, 37);

                for (int angle = 0; angle < 360; angle += 45)
                {
                    Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.right;
                    pivot.GetComponent<ActiveWeapon>().AimAt(pivot.transform.position + direction * 6);
                    Vector3 aimedRight = pivot.transform.TransformVector(Vector3.right).normalized;
                    Vector3 aimedUp = pivot.transform.TransformVector(Vector3.up).normalized;
                    Assert.That(Vector3.Dot(aimedRight, direction), Is.GreaterThan(0.99999f));
                    Assert.That(Vector3.Cross(aimedRight, aimedUp).z, Is.GreaterThan(0.99999f),
                        "Player mirroring must not reverse the animated swing or slash.");
                    Assert.That(Mathf.DeltaAngle(animatedSword.transform.localEulerAngles.z, 37),
                        Is.EqualTo(0).Within(0.001f), "Aiming must not overwrite the sword animation.");
                }
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void MouseAtPivotKeepsTheLastAim()
        {
            var root = new GameObject("Aim test", typeof(ActiveWeapon));
            try
            {
                var aim = root.GetComponent<ActiveWeapon>();
                aim.AimAt(Vector3.up);
                Quaternion before = root.transform.rotation;
                aim.AimAt(root.transform.position);
                Assert.That(Quaternion.Angle(before, root.transform.rotation), Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SharedPlayerPrefabHasCompleteCombatReferencesAndExistingAttackBinding()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dulli_roomtester.prefab");
            var sword = player.transform.Find("ActiveWeapon/Sword").GetComponent<Sword>();
            var settings = new SerializedObject(sword);
            var reference = settings.FindProperty("attackAction").objectReferenceValue as InputActionReference;
            Assert.That(reference, Is.Not.Null);
            Assert.That(reference.action.name, Is.EqualTo("Attack"));
            Assert.That(reference.action.actionMap.name, Is.EqualTo("Player"));
            Assert.That(AssetDatabase.GetAssetPath(reference), Is.EqualTo("Assets/Settings/InputSystem_Actions.inputactions"));
            Assert.That(reference.action.bindings.Any(b => b.path == "<Mouse>/leftButton"), Is.True);
            Assert.That(settings.FindProperty("hitPoint").objectReferenceValue, Is.Not.Null);
            Assert.That(settings.FindProperty("slashPrefab").objectReferenceValue, Is.Not.Null);
            var spawn = settings.FindProperty("slashAnimSpawnPoint").objectReferenceValue as Transform;
            Assert.That(spawn.parent, Is.EqualTo(sword.transform.parent),
                "The slash follows aim, without inheriting the sword's animated rotation again.");
            Assert.That(player.GetComponentsInChildren<Rigidbody2D>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponents<PlayerController2D>().Length, Is.EqualTo(1));
        }

        [TestCase("SwingUp", "SwingUpFlipAnim", AnimatorConditionMode.If)]
        [TestCase("SwingDown", "SwingDownFlipAnim", AnimatorConditionMode.IfNot)]
        public void SwingControllerHasTriggerTransitionsAndBoundHitWindowEvents(
            string stateName, string openEvent, AnimatorConditionMode directionCondition)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Combat/Sword.controller");
            var machine = controller.layers[0].stateMachine;
            Assert.That(machine.defaultState.name, Is.EqualTo("Idle"), "Entry should lead to Idle.");
            Assert.That(controller.parameters.Any(p => p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger), Is.True);
            var state = machine.states.Single(s => s.state.name == stateName).state;
            var transition = machine.defaultState.transitions.Single(t => t.destinationState == state);
            Assert.That(transition.conditions.Any(c => c.parameter == "Attack"), Is.True);
            Assert.That(transition.conditions.Any(c => c.parameter == "SwingUp" && c.mode == directionCondition), Is.True);
            Assert.That(state.transitions.Single().destinationState, Is.EqualTo(machine.defaultState));
            var clip = state.motion as AnimationClip;
            Assert.That(clip.length, Is.EqualTo(1).Within(0.001f));
            Assert.That(clip.isLooping, Is.False);
            var events = AnimationUtility.GetAnimationEvents(clip);
            Assert.That(events.Select(e => e.functionName), Is.EqualTo(new[] { openEvent, "CloseHitWindow", "FinishAttack" }));
            foreach (AnimationEvent e in events)
                Assert.That(typeof(Sword).GetMethod(e.functionName), Is.Not.Null);
            Assert.That(events[0].time, Is.LessThan(events[1].time));
            Assert.That(events[1].time, Is.LessThan(events[2].time));
        }

        [Test]
        public void SlashUsesAllNineSuppliedFramesAndHasCleanupEvent()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Combat/Slash.anim");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.That(frames.Select(f => f.value).Distinct().Count(), Is.EqualTo(9));
            Assert.That(frames.All(f => AssetDatabase.GetAssetPath(f.value) == "Assets/Sprites/Slash Effect.png"), Is.True);
            Assert.That(clip.isLooping, Is.False);
            var cleanup = AnimationUtility.GetAnimationEvents(clip).Single();
            Assert.That(cleanup.functionName, Is.EqualTo(nameof(SlashAnim.DestroySelf)));
            Assert.That(cleanup.time, Is.EqualTo(clip.length).Within(0.001f));
        }
    }
}
