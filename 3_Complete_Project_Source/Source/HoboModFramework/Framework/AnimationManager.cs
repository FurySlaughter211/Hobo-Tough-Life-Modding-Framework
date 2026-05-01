using UnityEngine;
using BepInEx.Logging;

namespace HoboModPlugin.Framework
{
    public class AnimationManager 
    {
        private Animator _animator;
        private ManualLogSource _log;

        public void Initialize(ManualLogSource log,string defaultStateName, Animator animator)
        {

            _log = log;

            _animator = animator;

            if (_animator == null)
            {
                _log.LogWarning("AnimationManager: Could not find an Animator component on this NPC");
                return;
            
            }

            if(!string.IsNullOrEmpty(defaultStateName))
            {
                _animator.Play(defaultStateName);
                _log.LogInfo($"AnimationManager: Playing default animation'{defaultStateName}'");



            }


        }
        

        
        
    } 


}

    


