using System.Linq;
using GravityRoguelike.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class ArcherTests
    {
        private const string Animations = "Assets/Animations/Player/VoidArcher/";

        [Test]
        public void ArcherPreservesMovementAndColliderWithoutSwordComponents()
        {
            var warrior = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Warrior.prefab");
            var archer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Archer.prefab");
            Assert.That(archer.GetComponent<PlayerController2D>(), Is.Not.Null);
            Assert.That(archer.GetComponent<BoxCollider2D>().size, Is.EqualTo(warrior.GetComponent<BoxCollider2D>().size));
            Assert.That(archer.GetComponent<BoxCollider2D>().offset, Is.EqualTo(warrior.GetComponent<BoxCollider2D>().offset));
            Assert.That(archer.transform.localScale, Is.EqualTo(warrior.transform.localScale));
            Assert.That(archer.GetComponentsInChildren<Sword>(true), Is.Empty);
            Assert.That(archer.GetComponentsInChildren<PlayerBodyAnimator>(true), Is.Empty);
            Assert.That(archer.GetComponentsInChildren<SlashAnim>(true), Is.Empty);
            Assert.That(archer.GetComponentInChildren<ArcherBodyAnimator>(), Is.Not.Null);
            Assert.That(archer.GetComponentInChildren<ActiveWeapon>(), Is.Not.Null);
            var bow = new SerializedObject(archer.GetComponentInChildren<ArcherBow>());
            foreach (string field in new[] { "weapon", "firePoint", "arrowPrefab" })
                Assert.That(bow.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }

        [TestCase("Idle", "VoidArcher_IdleNoBow.png", 6, true)]
        [TestCase("Run", "VoidArcher_Run.png", 8, true)]
        [TestCase("Jump Up", "VoidArcher_Jump.png", 4, false)]
        [TestCase("Fall", "VoidArcher_Jump.png", 1, true)]
        [TestCase("Land", "VoidArcher_Jump.png", 4, false)]
        [TestCase("Shoot", "VoidArcher_Body_Shoot.png", 8, false)]
        public void BodyUsesOnlyBowlessSprites(string name, string sheet, int count, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + name + ".anim");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            Assert.That(binding.path, Is.Empty);
            var sprites = AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(k => (Sprite)k.value).Distinct().ToArray();
            Assert.That(sprites.Length, Is.EqualTo(count));
            Assert.That(sprites.All(s => AssetDatabase.GetAssetPath(s) == "Assets/Sprites/Player/VoidArcher/" + sheet), Is.True);
            Assert.That(sprites.All(s => s.pixelsPerUnit == 16 && Vector2.Distance(s.pivot, new Vector2(21, 0)) < 0.001f), Is.True);
            Assert.That(clip.isLooping, Is.EqualTo(loop));
        }

        [TestCase("Jump Up", false, true, "Jump Up")]
        [TestCase("Fall", false, false, "Fall")]
        [TestCase("Jump Up", true, false, "Idle")]
        [TestCase("Land", true, false, "Idle")]
        public void ShootInterruptsAirStatesAndReturnsToLivePhysics(string initial, bool grounded, bool rising, string expected)
        {
            var root = new GameObject("Archer animation test", typeof(SpriteRenderer), typeof(Animator));
            try
            {
                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "Body.controller");
                animator.Rebind();
                animator.SetFloat("ShootSpeed", 1f);
                animator.SetBool("IsGrounded", initial == "Land");
                animator.SetBool("IsRising", initial == "Jump Up");
                animator.Play("Base Layer." + initial, 0, 0.4f);
                animator.Update(0f);
                animator.SetBool("Shooting", true);
                animator.SetTrigger("Shoot");
                animator.Update(0.01f); animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Shoot"), Is.True);
                animator.SetBool("IsGrounded", grounded);
                animator.SetBool("IsRising", rising);
                animator.SetBool("Shooting", false);
                animator.Update(0.01f); animator.Update(0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(expected), Is.True);
                if (expected == "Jump Up") Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ArrowReleaseIsAnEventOnTheSynchronizedBowClip()
        {
            var bow = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + "Bow Shoot.anim");
            var body = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + "Shoot.anim");
            Assert.That(bow.length, Is.EqualTo(1f).Within(0.001f));
            Assert.That(body.length, Is.EqualTo(bow.length));
            var events = AnimationUtility.GetAnimationEvents(bow);
            Assert.That(events.Select(e => e.functionName), Is.EqualTo(new[] { "ReleaseArrow", "FinishShot" }));
            Assert.That(events[0].time, Is.EqualTo(0.65f).Within(0.001f));
            var arrow = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/Arrow.prefab");
            Assert.That(arrow.GetComponent<Rigidbody2D>().gravityScale, Is.Zero);
            Assert.That(arrow.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(arrow.GetComponent<ArrowProjectile>().Speed, Is.GreaterThan(0));
        }

        [Test]
        public void VisibilityRejectsOffscreenHiddenAndCameraCulledTargets()
        {
            var cameraObject = new GameObject("Archer camera test", typeof(Camera));
            var target = new GameObject("Target", typeof(Health), typeof(SpriteRenderer));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 1;
                camera.transform.position = new Vector3(0, 0, -10);
                var renderer = target.GetComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Player/VoidArcher/VoidArcher_IdleNoBow.png").OfType<Sprite>().First();
                var health = target.GetComponent<Health>();
                Assert.That(ArrowProjectile.IsTargetVisible(camera, health), Is.True);
                target.transform.position = new Vector3(100, 0, 0);
                Assert.That(ArrowProjectile.IsTargetVisible(camera, health), Is.False);
                Assert.That(ArrowProjectile.IsPointInView(camera, target.transform.position, 0), Is.False);
                target.transform.position = Vector3.zero;
                renderer.enabled = false;
                Assert.That(ArrowProjectile.IsTargetVisible(camera, health), Is.False);
                renderer.enabled = true; camera.cullingMask = 0;
                Assert.That(ArrowProjectile.IsTargetVisible(camera, health), Is.False);
                camera.cullingMask = -1; camera.transform.rotation = Quaternion.Euler(0, 0, 90);
                Assert.That(ArrowProjectile.IsTargetVisible(camera, health), Is.True);
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject); }
        }
    }
}
