using UnityEngine;

namespace AlfaGiulia
{
    // Native light channels use the game's ShaderGraph. This bridge translates their
    // state into HDRP/Lit's color property without changing native brake/blinker logic.
    public sealed class AlfaGiuliaLighting : MonoBehaviour
    {
        public CarController Car;
        public MeshRenderer Front, Rear, Reverse, Left, Right;
        private MaterialPropertyBlock _properties;
        private CarFeatures _features;
        private MaterialPropertyBlock _blinkerState;
        private static readonly int Emission = Shader.PropertyToID("_EmissiveColor");
        private static readonly System.Reflection.PropertyInfo HeadlightState =
            typeof(VehicleController).GetProperty("ShouldLightsBeOn",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        private void LateUpdate()
        {
            if (Car == null || Car.vehicleController == null || !Car.vehicleController.IsInitialized) return;
            var vc = Car.vehicleController; var lights = vc.effectsManager.lightsManager;
            bool braking = Car.controlledByPlayer && (vc.brakes.IsBraking || vc.brakes.handbrakeValue > .025f);
            bool headlights = HeadlightState != null ? (bool)HeadlightState.GetValue(Car) : lights.lowBeamLights.On;
            ApplyStates(headlights,lights.highBeamLights.On,headlights,braking,
                Car.controlledByPlayer && vc.powertrain.transmission.Gear < 0,false,false);
            SyncBlinkers();
        }
        public void SyncBlinkers()
        {
            // VehicleBlinker writes the selected sides to the body and the actual
            // flash phase to its first renderer. Reuse that phase to match its audio.
            if (_features == null && Car != null) _features = Car.GetComponent<CarFeatures>();
            bool left = false, right = false;
            if (Car != null && Car.controlledByPlayer && _features != null &&
                _features.bodyMeshes != null && _features.bodyMeshes.Length > 0 && _features.bodyMeshes[0] != null)
            {
                if (_blinkerState == null) _blinkerState = new MaterialPropertyBlock();
                _blinkerState.Clear();
                _features.bodyMeshes[0].GetPropertyBlock(_blinkerState);
                bool flash = _blinkerState.GetInt("_IsBlinkerOn") == 1;
                left = flash && _blinkerState.GetInt("_IsLeftBlinkerOn") == 1;
                right = flash && _blinkerState.GetInt("_IsRightBlinkerOn") == 1;
            }
            Set(Left,left?new Color(5,1.1f,0):Color.black);
            Set(Right,right?new Color(5,1.1f,0):Color.black);
        }
        public void ApplyStates(bool low,bool high,bool tail,bool brake,bool reverse,bool left,bool right)
        {
            Set(Front,high?Color.white*8:low?Color.white*3:Color.black);
            Set(Rear,brake?new Color(5,.055f,.008f):tail?new Color(2,.025f,.005f):Color.black);
            Set(Reverse,reverse?Color.white*3:Color.black);
            Set(Left,left?new Color(5,1.1f,0):Color.black);
            Set(Right,right?new Color(5,1.1f,0):Color.black);
        }
        private void Set(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            if (_properties == null) _properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_properties); _properties.SetColor(Emission, color); renderer.SetPropertyBlock(_properties);
            // Native LightSource uses an indexed property block. It overrides the
            // renderer block completely, including properties absent from it.
            for(int i=0;i<renderer.sharedMaterials.Length;i++){
                renderer.GetPropertyBlock(_properties,i);
                _properties.SetColor(Emission,color);
                renderer.SetPropertyBlock(_properties,i);
            }
        }
    }
}
