using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// "Out of hearts" popup, shown when the player tries to start a level
    /// with 0 hearts. Three ways forward, in pedagogical order: practice for
    /// free (Review always runs without hearts and EARNS one back), wait for
    /// the timed regen, or spend coins for an instant refill. Ticks once a
    /// second while open so the countdown & heart row stay live — if a heart
    /// regenerates while the popup is open it closes itself.
    /// </summary>
    public class HeartsPopupUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;
        readonly Image[] _heartDots = new Image[HeartsManager.MaxHearts];
        Text _timerText;
        Text _refillLabel;
        Text _gemRefillLabel;

        public HeartsPopupUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "HeartsPopup");
            Build();
            _root.gameObject.SetActive(false);
        }

        void Build()
        {
            var t = _root.transform;

            // Full-screen dim that swallows clicks under the card.
            var dim = UIFactory.CreatePanel(t, "Dim", new Color(0.10f, 0.07f, 0.16f, 0.55f), 0f);
            dim.raycastTarget = true;

            var card = UIFactory.CreatePanel(t, "Card", Theme.CreamPanel, 24f);
            UIFactory.AddSoftShadow(card.gameObject, 5f, 0.25f);
            UIFactory.BandTop(card.rectTransform, 180f, 396f, 30f);
            var ct = card.transform;

            var title = UIFactory.CreateText(ct, "Title", Loc.T("hearts.out"), 22, Theme.BadRed);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 18f, 30f);

            // Heart row.
            const float dotSize = 30f;
            const float dotGap = 10f;
            float rowW = HeartsManager.MaxHearts * dotSize + (HeartsManager.MaxHearts - 1) * dotGap;
            for (int i = 0; i < HeartsManager.MaxHearts; i++)
            {
                var dot = UIFactory.CreateIcon(ct, "Heart" + i, "heart", Theme.BadRed);
                var rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(dotSize, dotSize);
                rt.anchoredPosition = new Vector2(
                    -rowW * 0.5f + dotSize * 0.5f + i * (dotSize + dotGap), -76f);
                _heartDots[i] = dot;
            }

            _timerText = UIFactory.CreateText(ct, "Timer", "", 15, Theme.InkSoft);
            UIFactory.BandTop(_timerText.rectTransform, 100f, 22f);

            // 1) Practice — free, earns a heart.
            var practice = UIFactory.CreateButton(ct, "Practice", Loc.T("hearts.practice"), 16,
                Theme.Teal, Color.white,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    Hide();
                    _boot.StartRun(true);
                });
            UIFactory.BandTop(practice.GetComponent<RectTransform>(), 136f, 48f, 24f);

            // 2) Coin refill.
            var refill = UIFactory.CreateButton(ct, "Refill", "", 16,
                Theme.Amber, Theme.WordChipDark,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (HeartsManager.TryCoinRefill())
                    {
                        ProceduralAudio.PlayCorrect();
                        Hide();
                        _boot.ShowHome();
                    }
                    else if (_timerText != null)
                    {
                        _timerText.text = Loc.F("hearts.no_coins", ProgressManager.Coins);
                    }
                });
            UIFactory.BandTop(refill.GetComponent<RectTransform>(), 192f, 48f, 24f);
            _refillLabel = refill.GetComponentInChildren<Text>();

            // 3) Gem refill — the premium currency's shortcut.
            var gemRefill = UIFactory.CreateButton(ct, "GemRefill", "", 16,
                Theme.Purple, Color.white,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (HeartsManager.TryGemRefill())
                    {
                        ProceduralAudio.PlayCorrect();
                        Hide();
                        _boot.ShowHome();
                    }
                    else if (_timerText != null)
                    {
                        _timerText.text = Loc.F("hearts.no_gems", ProgressManager.Gems);
                    }
                });
            UIFactory.BandTop(gemRefill.GetComponent<RectTransform>(), 248f, 48f, 24f);
            _gemRefillLabel = gemRefill.GetComponentInChildren<Text>();

            // 4) Wait / close.
            var close = UIFactory.CreateButton(ct, "Close", Loc.T("hearts.later"), 15,
                Theme.CreamPanel, Theme.InkSoft,
                delegate { ProceduralAudio.PlayClick(); Hide(); });
            UIFactory.BandTop(close.GetComponent<RectTransform>(), 304f, 42f, 24f);

            var note = UIFactory.CreateText(ct, "Note",
                Loc.T("hearts.note"), 13, Theme.InkSoft);
            UIFactory.BandTop(note.rectTransform, 352f, 32f);
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            RefreshNow();
            _boot.RunRoutine(Tick());
        }

        public void Hide() { _root.gameObject.SetActive(false); }

        void RefreshNow()
        {
            int hearts = HeartsManager.Hearts;
            for (int i = 0; i < _heartDots.Length; i++)
                _heartDots[i].color = i < hearts
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.18f);

            int secs = HeartsManager.SecondsToNextHeart;
            _timerText.text = hearts >= HeartsManager.MaxHearts
                ? Loc.T("hearts.full")
                : Loc.F("hearts.next_in", (secs / 60) + ":" + (secs % 60).ToString("00"));

            _refillLabel.text = Loc.F("hearts.refill_coins", HeartsManager.RefillAllCost);
            if (_gemRefillLabel != null)
                _gemRefillLabel.text = Loc.F("hearts.refill_gems", GameConfig.HeartRefillGems, ProgressManager.Gems);
        }

        IEnumerator Tick()
        {
            var wait = new WaitForSeconds(1f);
            while (_root.gameObject.activeSelf)
            {
                RefreshNow();
                // A heart came back while waiting — let the player go play.
                if (HeartsManager.Hearts > 0)
                {
                    yield return new WaitForSeconds(0.6f);
                    if (_root.gameObject.activeSelf)
                    {
                        Hide();
                        _boot.ShowHome();
                    }
                    yield break;
                }
                yield return wait;
            }
        }
    }
}
