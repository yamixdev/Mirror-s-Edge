namespace MEdge.Source
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>Owner-only first-person meshes and a complete body for other cameras.</summary>
    public sealed class PlayerBodyVisibility : MonoBehaviour
    {
        SkinnedMeshRenderer _upper, _lower, _body;
        Camera _ownerCamera;
        State _original;
        readonly Stack<State> _renderStates = new();
        bool _configured;

        struct State
        {
            public Camera Camera;
            public bool Upper, Lower, Body;
            public ShadowCastingMode UpperShadow, LowerShadow, BodyShadow;
        }

        public void Configure(SkinnedMeshRenderer upper, SkinnedMeshRenderer lower, SkinnedMeshRenderer body, Camera ownerCamera)
        {
            if (_configured) Restore(_original);
            _renderStates.Clear();
            _upper = upper; _lower = lower; _body = body; _ownerCamera = ownerCamera;
            var materials = body.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (!materials[i]) continue;
                // The FBX leaves the teeth slot unnamed (Unity imports it as material_0).
                var materialName = materials[i].name == "material_0" ? "faithTeeth" : materials[i].name;
                var restored = Resources.Load<Material>("LocalFaith3P/" + materialName);
                if (restored) materials[i] = restored;
            }
            body.sharedMaterials = materials;
            _original = Capture(null);
            _configured = true;
            ShowExternalBody();
        }

        public void SetOwnerCamera(Camera camera) => _ownerCamera = camera;

        void OnEnable()
        {
            Camera.onPreCull += BeforeCamera;
            Camera.onPostRender += AfterCamera;
            if (_configured && _upper && _lower && _body) ShowExternalBody();
        }

        void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            Camera.onPostRender -= AfterCamera;
            _renderStates.Clear();
            if (_configured) Restore(_original);
        }

        State Capture(Camera camera) => new()
        {
            Camera = camera,
            Upper = _upper.enabled, Lower = _lower.enabled, Body = _body.enabled,
            UpperShadow = _upper.shadowCastingMode, LowerShadow = _lower.shadowCastingMode, BodyShadow = _body.shadowCastingMode
        };

        void Restore(State state)
        {
            if (_upper) { _upper.enabled = state.Upper; _upper.shadowCastingMode = state.UpperShadow; }
            if (_lower) { _lower.enabled = state.Lower; _lower.shadowCastingMode = state.LowerShadow; }
            if (_body) { _body.enabled = state.Body; _body.shadowCastingMode = state.BodyShadow; }
        }

        void ShowExternalBody()
        {
            _upper.enabled = _lower.enabled = false;
            _upper.shadowCastingMode = _lower.shadowCastingMode = ShadowCastingMode.Off;
            _body.enabled = true;
            _body.shadowCastingMode = ShadowCastingMode.On;
        }

        void BeforeCamera(Camera camera)
        {
            if (!_configured || !_upper || !_lower || !_body) return;
            // Preserve the outer camera's state when a reflection camera renders recursively.
            _renderStates.Push(Capture(camera));
            ShowExternalBody();
            if (_ownerCamera && camera == _ownerCamera)
            {
                _upper.enabled = _lower.enabled = true;
                _body.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        void AfterCamera(Camera camera)
        {
            if (_renderStates.Count > 0 && _renderStates.Peek().Camera == camera)
                Restore(_renderStates.Pop());
        }
    }
}
