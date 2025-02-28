﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalEngine
{
    /// <summary>
    /// Basic class for any type of slot (item, other)
    /// </summary>

    public class UISlot : MonoBehaviour
    {
        [Header("Navigation")] //Leave empty for default navigation
        public UISlot top;
        public UISlot down;
        public UISlot left;
        public UISlot right;

        [HideInInspector]
        public int index = -1;

        public UnityAction<UISlot> onClick;
        public UnityAction<UISlot> onClickRight;
        public UnityAction<UISlot> onClickLong;
        public UnityAction<UISlot> onClickDouble;
        public UnityAction<UISlot> onPressKey; //Numerical key
        public UnityAction<UISlot> onPressAccept;
        public UnityAction<UISlot> onPressCancel;
        public UnityAction<UISlot> onPressUse;

        protected Button button;
        protected EventTrigger evt_trigger;
        protected RectTransform rect;
        protected UISlotPanel parent;

        protected bool selected = false;
        protected bool key_hover = false;

        private bool is_holding = false;
        private bool can_click = false;
        private float holding_timer = 0f;
        private float double_timer = 0f;

        protected virtual void Awake()
        {
            parent = GetComponentInParent<UISlotPanel>();
            rect = GetComponent<RectTransform>();
            evt_trigger = GetComponent<EventTrigger>();
            button = GetComponent<Button>();
        }

        protected virtual void Start()
        {
            if (evt_trigger != null)
            {
                EventTrigger.Entry entry1 = new EventTrigger.Entry();
                entry1.eventID = EventTriggerType.PointerClick;
                entry1.callback.AddListener((BaseEventData eventData) => { OnClick(eventData); });
                evt_trigger.triggers.Add(entry1);

                EventTrigger.Entry entry2 = new EventTrigger.Entry();
                entry2.eventID = EventTriggerType.PointerDown;
                entry2.callback.AddListener((BaseEventData eventData) => { OnDown(eventData); });
                evt_trigger.triggers.Add(entry2);

                EventTrigger.Entry entry3 = new EventTrigger.Entry();
                entry3.eventID = EventTriggerType.PointerUp;
                entry3.callback.AddListener((BaseEventData eventData) => { OnUp(eventData); });
                evt_trigger.triggers.Add(entry3);

                EventTrigger.Entry entry4 = new EventTrigger.Entry();
                entry4.eventID = EventTriggerType.PointerExit;
                entry4.callback.AddListener((BaseEventData eventData) => { OnExit(eventData); });
                evt_trigger.triggers.Add(entry4);
            }

            if (button != null)
            {
                button.onClick.AddListener(ClickSlot);
            }
        }

        protected virtual void Update()
        {
            if (double_timer < 1f)
                double_timer += Time.deltaTime;

            //Hold
            if (is_holding)
            {
                holding_timer += Time.deltaTime;
                if (holding_timer > 0.5f)
                {
                    can_click = false;
                    is_holding = false;
                    if (onClickLong != null)
                        onClickLong.Invoke(this);
                }
            }

            //Keyboard shortcut
            int key_index = (index + 1);
            if (key_index == 10)
                key_index = 0;
            if (key_index < 10 && Input.GetKeyDown(key_index.ToString()))
            {
                if (onPressKey != null)
                    onPressKey.Invoke(this);
            }

            bool use_mouse = PlayerControlsMouse.Get().IsUsingMouse();
            key_hover = false;
            foreach (KeyControlsUI kcontrols in KeyControlsUI.GetAll())
            {
                bool hover = !use_mouse && kcontrols != null && kcontrols.GetFocusedPanel() == parent
                    && index >= 0 && kcontrols.GetSelectedIndex() == index;
                key_hover = key_hover || hover;
            }
        }

        public void SelectSlot()
        {
            selected = true;
        }

        public void UnselectSlot()
        {
            selected = false;
        }

        public bool IsSelected()
        {
            return selected;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void ClickSlot()
        {
            if (onClick != null)
                onClick.Invoke(this);
        }

        public void ClickRightSlot()
        {
            if (onClickRight != null)
                onClickRight.Invoke(this);
        }

        public void KeyPressAccept()
        {
            if (onPressAccept != null)
                onPressAccept.Invoke(this);
        }

        public void KeyPressCancel()
        {
            if (onPressCancel != null)
                onPressCancel.Invoke(this);
        }

        public void KeyPressUse()
        {
            if (onPressUse != null)
                onPressUse.Invoke(this);
        }

        void OnClick(BaseEventData eventData)
        {
            if (can_click)
            {

            }
        }

        void OnDown(BaseEventData eventData)
        {
            is_holding = true;
            can_click = true;
            holding_timer = 0f;

            PointerEventData pEventData = eventData as PointerEventData;

            if (pEventData.button == PointerEventData.InputButton.Right)
            {
                if (onClickRight != null)
                    onClickRight.Invoke(this);
            }
            else if (pEventData.button == PointerEventData.InputButton.Left)
            {
                if (double_timer < 0f)
                {
                    double_timer = 0f;
                    if (onClickDouble != null)
                        onClickDouble.Invoke(this);
                }
                else
                {
                    double_timer = -0.3f;
                    if (onClick != null)
                        onClick.Invoke(this);
                }
            }
        }

        void OnUp(BaseEventData eventData)
        {
            is_holding = false;
        }

        void OnExit(BaseEventData eventData)
        {
            is_holding = false;
        }

        public bool IsVisible()
        {
            return gameObject.activeSelf && (parent == null || parent.IsVisible());
        }

        public RectTransform GetRect()
        {
            return rect;
        }

        public UISlotPanel GetParent()
        {
            return parent;
        }
    }

}