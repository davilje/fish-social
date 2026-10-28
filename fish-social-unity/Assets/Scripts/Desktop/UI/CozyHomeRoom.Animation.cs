using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public sealed partial class CozyHomeRoom
    {
        [SerializeField] RawImage foodLayer, pourLayer;
        [SerializeField] Image bowlView;
        CozyHomeAnimation.Clip animationClip;
        float animationTime, interactionTime;
        string facing = "right";
        int foodState;
        public string AnimationAction => animationClip?.action;
        public int AnimationFrame => animationClip == null ? -1 : animationClip.FrameAt(animationTime, activity == Activity.Eating ? 3 : 1, animationClip.action == "walk");
        public bool IsFeeding => activity == Activity.Pouring || activity == Activity.GoingToFood || activity == Activity.Eating;
        public bool HasFeedingAnimation => CozyHomeAnimation.CanFeed(skin);

        void EnsureAnimationLayers()
        {
            var bowl = Prop("food-bowls");
            if (bowlView == null)
            {
                var rect = Rect(transform, "food-bowls", 0, 0, 1, 1);
                bowlView = rect.gameObject.AddComponent<Image>();
                rect.gameObject.AddComponent<CozyRoomBowlView>().Configure(bowl);
            }
            bowlView.sprite = bowl.GetComponent<Image>().sprite;
            bowlView.raycastTarget = true;
            bowl.GetComponent<Image>().enabled = false;
            bowlView.transform.SetSiblingIndex(actor.GetSiblingIndex() + 1);
            if (foodLayer == null) foodLayer = AnimationLayer("FoodLayer");
            if (pourLayer == null) pourLayer = AnimationLayer("FoodPour");
            PresentFood();
        }

        RawImage AnimationLayer(string name)
        {
            var existing = transform.Find(name);
            var image = existing == null ? Rect(transform, name, 0, 0, 1, 1).gameObject.AddComponent<RawImage>() : existing.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        void PlayAnimation(CozyHomeAnimation.Clip clip)
        {
            animationClip = clip;
            animationTime = 0;
        }

        void CancelFeeding()
        {
            if (!IsFeeding) return;
            CancelAnimation();
            Present();
        }

        void CancelAnimation()
        {
            if (activity == Activity.Rolling || IsFeeding) activity = Activity.Free;
            animationClip = null; animationTime = interactionTime = 0; foodState = 0;
            target = feet;
            if (foodLayer != null) foodLayer.gameObject.SetActive(false);
            if (pourLayer != null) pourLayer.gameObject.SetActive(false);
        }

        public void Feed()
        {
            if (!CanInteract || IsFeeding) return;
            RefreshSkin();
            if (!HasFeedingAnimation)
            {
                feedback = 2; DesktopLocalization.SetText(bubble, DesktopLocalization.Text("ui.home.feeding.status.animation_pending", "这位伙伴的进食动作正在准备中"));
                bubble.transform.parent.gameObject.SetActive(true);
                return;
            }
            CancelAnimation();
            EnsureAnimationLayers();
            dragging = false; pointer = int.MinValue; sitting = false;
            inactive = wander = feedback = 0;
            activity = Activity.Pouring;
            if (CozyMotion.Reduced) { interactionTime = 1; BeginApproachFood(); }
            Present();
        }

        Vector2 FoodContact()
        {
            var bowl = (RectTransform)Prop("food-bowls").transform;
            return new Vector2(bowl.anchoredPosition.x, -bowl.anchoredPosition.y) + new Vector2(76f / 270f * bowl.rect.width, 24f / 88f * bowl.rect.height);
        }

        Vector2 FoodDestination()
        {
            return FoodContact() - (CozyHomeAnimation.MouthContact(facing) - new Vector2(256, 352)) * CozyHomeAnimation.CharacterScale;
        }

        void BeginApproachFood()
        {
            foodState = 1;
            facing = feet.x > FoodContact().x ? "left" : "right";
            target = FoodDestination();
            activity = Activity.GoingToFood;
            if (CozyMotion.Reduced) { feet = target; BeginEating(); }
        }

        void BeginEating()
        {
            activity = Activity.Eating;
            PlayAnimation(CozyHomeAnimation.Find(skin, "eat", facing));
            interactionTime = 0;
        }

        bool AdvanceInteraction(float delta)
        {
            if (activity == Activity.Rolling)
            {
                if (CozyMotion.Reduced) { CancelAnimation(); return true; }
                animationTime += delta;
                if (animationTime >= animationClip.Duration) { animationTime = animationClip.Duration; activity = Activity.Free; inactive = wander = 0; }
                return true;
            }
            if (activity == Activity.Pouring)
            {
                interactionTime += delta;
                if (CozyMotion.Reduced || interactionTime >= CozyHomeAnimation.Find("shared", "food-pour").Duration) BeginApproachFood();
                return true;
            }
            if (activity == Activity.GoingToFood)
            {
                target = FoodDestination();
                AdvanceLocomotion(delta, 180);
                if ((feet - target).sqrMagnitude < .01f) BeginEating();
                return true;
            }
            if (activity != Activity.Eating) return false;
            animationTime += delta;
            foodState = animationTime < 11f / 12f ? 1 : animationTime < 19f / 12f ? 2 : animationTime < 27f / 12f ? 3 : 0;
            if (animationTime >= animationClip.PlaybackDuration(3))
            {
                CancelAnimation(); activity = Activity.Free; target = ClampFeet(feet); inactive = wander = 0;
            }
            return true;
        }

        void AdvanceLocomotion(float delta, float speed)
        {
            var distance = Vector2.Distance(feet, target);
            if (CozyMotion.Reduced || !CozyHomeAnimation.CanWalk(skin))
            {
                if (CozyMotion.Reduced) animationClip = null;
                feet = CozyMotion.Reduced ? target : Vector2.MoveTowards(feet, target, speed * delta);
                return;
            }
            if (distance > .1f)
            {
                var direction = Mathf.Abs(target.x - feet.x) < .1f ? facing : target.x < feet.x ? "left" : "right";
                if (animationClip == null || animationClip.action != "walk-start" && animationClip.action != "walk" || facing != direction)
                {
                    facing = direction;
                    PlayAnimation(CozyHomeAnimation.Find(skin, "walk-start", facing));
                }
                animationTime += delta;
                if (animationClip.action == "walk-start")
                {
                    if (animationTime < animationClip.Duration) return;
                    PlayAnimation(CozyHomeAnimation.Find(skin, "walk", facing));
                }
                feet = Vector2.MoveTowards(feet, target, speed * delta);
                if ((feet-target).sqrMagnitude < .01f && activity == Activity.Free) PlayAnimation(CozyHomeAnimation.Find(skin, "walk-stop", facing));
            }
            else if (animationClip != null && animationClip.action == "walk-stop") animationTime = Mathf.Min(animationTime + delta, animationClip.Duration);
        }

        void PresentAnimation()
        {
            var clip = animationClip;
            var frame = CozyMotion.Reduced && activity == Activity.Eating ? 3 : AnimationFrame;
            artwork.texture = clip.textures[frame]; artwork.uvRect = new Rect(0, 0, 1, 1);
            actor.sizeDelta = new Vector2(clip.canvas[0], clip.canvas[1]) * CozyHomeAnimation.CharacterScale;
            actor.anchoredPosition = new Vector2(feet.x - clip.registration[0] * CozyHomeAnimation.CharacterScale, -feet.y + clip.registration[1] * CozyHomeAnimation.CharacterScale);
        }

        void PresentFood()
        {
            if (foodLayer == null || pourLayer == null) return;
            var bowl = (RectTransform)Prop("food-bowls").transform;
            if (bowlView != null)
            {
                bowlView.gameObject.SetActive(!pageOpen);
                bowlView.rectTransform.anchoredPosition = bowl.anchoredPosition;
                bowlView.rectTransform.sizeDelta = bowl.rect.size;
            }
            var visible = IsFeeding && !pageOpen && focused;
            foodLayer.gameObject.SetActive(visible);
            pourLayer.gameObject.SetActive(visible && activity == Activity.Pouring && !CozyMotion.Reduced);
            if (!visible) return;
            foodLayer.rectTransform.anchoredPosition = bowl.anchoredPosition;
            foodLayer.rectTransform.sizeDelta = bowl.rect.size;
            var fill = CozyHomeAnimation.Find("shared", "food-fill");
            var states = CozyHomeAnimation.Find("shared", "bowl-food");
            foodLayer.texture = activity == Activity.Pouring ? fill.textures[fill.FrameAt(interactionTime)] : states.textures[foodState];
            if (pourLayer.gameObject.activeSelf)
            {
                var pour = CozyHomeAnimation.Find("shared", "food-pour");
                var contact = FoodContact();
                pourLayer.rectTransform.sizeDelta = new Vector2(240, 200);
                pourLayer.rectTransform.anchoredPosition = new Vector2(contact.x - 86, -contact.y + 172);
                pourLayer.texture = pour.textures[pour.FrameAt(interactionTime)];
            }
        }
    }
}
