using System.Linq;
using GravityRoguelike.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class MageTests
    {
        private const string Animations = "Assets/Animations/Player/VoidMage/";

        private static Animator MakeAnimator(string controller, string name)
        {
            var root = new GameObject(name, typeof(SpriteRenderer), typeof(Animator));
            if (controller == "Staff")
            {
                new GameObject("Visual", typeof(SpriteRenderer)).transform.SetParent(root.transform, false);
                new GameObject("FirePoint").transform.SetParent(root.transform, false);
            }
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + controller + ".controller");
            animator.fireEvents = false; // These tests evaluate transitions, not projectile-spawning events.
            animator.Rebind();
            animator.SetBool("IsGrounded", true);
            animator.Play("Idle", 0, 0f);
            Step(animator);
            return animator;
        }

        private static void Step(Animator animator, float seconds = 0.01f)
        {
            animator.Update(seconds);
            animator.Update(0f);
        }

        private static void State(Animator animator, string expected) =>
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(expected), Is.True, expected);

        [Test]
        public void BodyMovementAndAirOverrideCastingWhileStaffContinues()
        {
            var body = MakeAnimator("Body", "Mage body test");
            var staff = MakeAnimator("Staff", "Mage staff test");
            try
            {
                staff.SetBool("IsCastingA", true); staff.SetTrigger("CastA"); Step(staff); Step(staff, 0.4f);
                State(staff, "A Hold");
                body.SetBool("IsCastingA", true); Step(body); State(body, "Spell A Body");
                body.SetBool("Running", true); Step(body); State(body, "Run");
                body.SetBool("IsGrounded", false); body.SetBool("IsRising", true); Step(body); State(body, "Jump Up");
                Step(body, 0.6f); State(body, "Jump Up");
                Assert.That(body.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(1f));
                body.SetBool("IsRising", false); Step(body); State(body, "Fall");
                body.SetBool("IsGrounded", true); body.SetBool("Running", false); Step(body); State(body, "Land");
                Step(body, 0.3f); State(body, "Spell A Body");
                State(staff, "A Hold");
            }
            finally { Object.DestroyImmediate(body.gameObject); Object.DestroyImmediate(staff.gameObject); }
        }

        [Test]
        public void SpellAEndsInIdleInAirAndBackResetsForTheNextJump()
        {
            var staff = MakeAnimator("Staff", "Mage A test");
            try
            {
                staff.SetBool("IsGrounded", false); Step(staff); State(staff, "OnBack");
                staff.SetBool("HasCastThisAirtime", true);
                staff.SetBool("IsCastingA", true); staff.SetTrigger("CastA"); Step(staff); State(staff, "A Raise");
                Step(staff, 0.4f); State(staff, "A Hold");
                staff.SetBool("IsCastingA", false); Step(staff); State(staff, "A Retract");
                Step(staff, 0.4f); State(staff, "Idle");
                staff.SetBool("IsGrounded", true); staff.SetBool("HasCastThisAirtime", false); Step(staff); State(staff, "Idle");
                staff.SetBool("IsGrounded", false); Step(staff); State(staff, "OnBack");
            }
            finally { Object.DestroyImmediate(staff.gameObject); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SpellBPreemptsAAndTapCompletesItsCast(bool waitForHold)
        {
            var staff = MakeAnimator("Staff", "Mage B test");
            try
            {
                staff.SetBool("IsCastingA", true); staff.SetTrigger("CastA"); Step(staff); State(staff, "A Raise");
                staff.SetBool("IsHoldingB", true); staff.SetBool("IsCastingA", false); staff.SetTrigger("CastB");
                Step(staff); State(staff, "B Cast");
                if (waitForHold) { Step(staff, 1.2f); State(staff, "B Hold"); }
                staff.SetBool("IsHoldingB", false); Step(staff);
                if (!waitForHold) { State(staff, "B Cast"); Step(staff, 1.18f); }
                State(staff, "B Retract");
                Step(staff, 0.4f); State(staff, "Idle");
                staff.SetBool("IsCastingA", true); staff.SetTrigger("CastA"); Step(staff); State(staff, "A Raise");
            }
            finally { Object.DestroyImmediate(staff.gameObject); }
        }

        [Test]
        public void SpellBBodyOverridesRunButNeverAirOrLanding()
        {
            var body = MakeAnimator("Body", "Mage B body test");
            try
            {
                body.SetBool("Running", true); Step(body); State(body, "Run");
                body.SetBool("IsCastingB", true); Step(body); State(body, "Spell B Body");
                body.SetBool("IsLoopingB", true); Step(body); State(body, "Spell B Loop");
                body.SetBool("IsGrounded", false); body.SetBool("IsRising", true); Step(body); State(body, "Jump Up");
                body.SetBool("IsRising", false); Step(body); State(body, "Fall");
                body.SetBool("IsGrounded", true); Step(body); State(body, "Land");
                Step(body, 0.3f); State(body, "Spell B Loop");
                body.SetBool("IsCastingB", false); Step(body); State(body, "Run");
            }
            finally { Object.DestroyImmediate(body.gameObject); }
        }

        [Test]
        public void StaffIsBehindExceptForTheTwoSpellBSwingStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "Staff.controller");
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                var curve = AnimationUtility.GetEditorCurve((AnimationClip)child.state.motion,
                    EditorCurveBinding.FloatCurve("Visual", typeof(SpriteRenderer), "m_SortingOrder"));
                bool front = child.state.name == "B Cast" || child.state.name == "B Hold";
                Assert.That(curve.Evaluate(0), Is.EqualTo(front ? 11 : 9), child.state.name);
            }
        }

        [Test]
        public void SweepDealsDamageOncePerTargetAndSkipsOffscreenTargets()
        {
            var owner = new GameObject("Sweep owner", typeof(Rigidbody2D), typeof(MageSpellArea));
            var enemy = new GameObject("Sweep target", typeof(Health), typeof(BoxCollider2D), typeof(SpriteRenderer));
            var cameraObject = new GameObject("Sweep test camera", typeof(Camera));
            try
            {
                owner.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 1;
                camera.transform.position = new Vector3(0, 0, -10);
                enemy.layer = 31;
                enemy.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/Sprites/Player/VoidMage/Void Mage_Spell A_Body ONLY.png").OfType<Sprite>().First();
                var extra = new GameObject("Second collider", typeof(BoxCollider2D));
                extra.layer = 31; extra.transform.SetParent(enemy.transform, false);
                var health = enemy.GetComponent<Health>();
                typeof(Health).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(health, null);
                var area = owner.GetComponent<MageSpellArea>();
                var settings = new SerializedObject(area);
                settings.FindProperty("owner").objectReferenceValue = owner.transform;
                settings.FindProperty("viewCamera").objectReferenceValue = camera;
                settings.FindProperty("targetLayers").intValue = 1 << 31;
                settings.FindProperty("blockingLayers").intValue = 0;
                foreach (string field in new[] { "leftSweep", "rightSweep" })
                {
                    var shape = new GameObject(field, typeof(PolygonCollider2D));
                    shape.transform.SetParent(owner.transform, false);
                    var collider = shape.GetComponent<PolygonCollider2D>();
                    collider.isTrigger = true;
                    collider.SetPath(0, new[] { new Vector2(-2, -2), new Vector2(2, -2), new Vector2(2, 2), new Vector2(-2, 2) });
                    settings.FindProperty(field).objectReferenceValue = collider;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                area.BeginSweep(0);
                Assert.That(health.CurrentHealth, Is.EqualTo(1), "Both target colliders share one hit.");
                typeof(MageSpellArea).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(area, null);
                Assert.That(health.CurrentHealth, Is.EqualTo(1), "A continuing sweep must not hit twice.");
                camera.transform.position = new Vector3(100, 0, -10);
                area.BeginSweep(1);
                Assert.That(health.CurrentHealth, Is.EqualTo(1), "Offscreen target must not take damage.");
                camera.transform.position = new Vector3(0, 0, -10);
                area.BeginSweep(1);
                Assert.That(health.CurrentHealth, Is.Zero, "A new visible sweep can hit again.");
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(enemy); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void SpellBAreaUsesThePlayerBodyAndVisibleSweepEvents()
        {
            var mage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mage.prefab");
            var area = mage.GetComponentInChildren<MageSpellArea>(true);
            Assert.That(area, Is.Not.Null);
            foreach (var collider in area.GetComponentsInChildren<PolygonCollider2D>(true))
            {
                Assert.That(collider.isTrigger, Is.True);
                Assert.That(collider.enabled, Is.False);
                Assert.That(collider.attachedRigidbody, Is.EqualTo(mage.GetComponent<Rigidbody2D>()));
                Assert.That(collider.GetPath(0).Length, Is.GreaterThanOrEqualTo(3));
            }
            foreach (string name in new[] { "B Swing", "B Swing Loop" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + name + ".anim");
                var events = AnimationUtility.GetAnimationEvents(clip);
                Assert.That(events.Select(e => e.functionName), Is.EqualTo(new[] {
                    "BeginSpellBSweep", "EndSpellBSweep", "BeginSpellBSweep", "EndSpellBSweep" }));
                Assert.That(events[0].intParameter, Is.Zero);
                Assert.That(events[2].intParameter, Is.EqualTo(1));
            }
        }

        [Test]
        public void MageHasIndependentControllersAndNoAimedWeapons()
        {
            var mage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mage.prefab");
            var archer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ArcherBackup.prefab");
            Assert.That(mage.GetComponent<BoxCollider2D>().size, Is.EqualTo(archer.GetComponent<BoxCollider2D>().size));
            Assert.That(mage.GetComponent<PlayerController2D>(), Is.Not.Null);
            Assert.That(mage.GetComponentsInChildren<ActiveWeapon>(true), Is.Empty);
            Assert.That(mage.GetComponentsInChildren<Sword>(true), Is.Empty);
            Assert.That(mage.GetComponentsInChildren<ArcherBow>(true), Is.Empty);
            Assert.That(mage.GetComponentsInChildren<Animator>(true).Length, Is.EqualTo(2));
            var body = mage.transform.Find("Body"); var staff = mage.transform.Find("Staff");
            Assert.That(body.GetComponent<Animator>().runtimeAnimatorController,
                Is.Not.EqualTo(staff.GetComponent<Animator>().runtimeAnimatorController));
            Assert.That(body.localScale.y * 28f / body.GetComponent<SpriteRenderer>().sprite.pixelsPerUnit,
                Is.EqualTo(35f / 16f).Within(0.001f));
            Assert.That(staff.Find("FirePoint"), Is.Not.Null);
        }

        [TestCase("SpellA_Orb")]
        public void SpellsReuseTheExistingViewportAndDamageImplementation(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/" + name + ".prefab");
            Assert.That(prefab.GetComponent<ArrowProjectile>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Rigidbody2D>().gravityScale, Is.Zero);
            Assert.That(prefab.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            var projectile = new SerializedObject(prefab.GetComponent<ArrowProjectile>());
            foreach (string field in new[] { "speed", "damage", "lifetime", "viewportMargin" })
                Assert.That(projectile.FindProperty(field), Is.Not.Null, field);
            Assert.That(AssetDatabase.GetAssetPath(prefab.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite),
                Does.StartWith("Assets/Sprites/Player/VoidMage/"));
        }

        [Test]
        public void StaffReleaseAndLoopsHaveTheExpectedBindings()
        {
            var raise = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + "A Raise.anim");
            Assert.That(AnimationUtility.GetAnimationEvents(raise).Single().functionName, Is.EqualTo("ReleaseSpellA"));
            foreach (string name in new[] { "A Hold", "B Hold", "OnBack" })
                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + name + ".anim").isLooping, Is.True);
            foreach (string name in new[] { "A Retract", "B Retract", "Jump Up", "Land" })
                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + name + ".anim").isLooping, Is.False);
            Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(raise).Single().path, Is.EqualTo("Visual"));
            Assert.That(AnimationUtility.GetCurveBindings(raise).Any(b => b.path == "FirePoint"), Is.True);
        }
    }
}
