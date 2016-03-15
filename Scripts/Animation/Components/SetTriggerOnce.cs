﻿//----------------------------------------------
// Flip Web Apps: Game Framework
// Copyright © 2016 Flip Web Apps / Mark Hewitt
//----------------------------------------------

using FlipWebApps.GameFramework.Scripts.GameObjects.Components;
using UnityEngine;

namespace FlipWebApps.GameFramework.Scripts.Animation.Components
{
    /// <summary>
    /// Set an animation trigger only one time and optionally after another animation has already been triggered
    /// </summary>
    public class SetTriggerOnce : RunOnceGameObject
    {
        public Animator Animator;
        public string Trigger;

        public override void RunOnce()
        {
            Animator.SetTrigger(Trigger);
        }
    }
}