﻿using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine.ProBuilder.MeshOperations;

namespace UnityEngine.ProBuilder.Shapes
{
    sealed class ShapeComponent : MonoBehaviour
    {
        [Serializable]
        class ShapeBoxProperties
        {
            [SerializeField]
            internal float m_Width ;
            [SerializeField]
            internal float m_Length ;
            [SerializeField]
            internal float m_Height ;
        }

        [SerializeReference]
        Shape m_Shape = new Cube();

        [SerializeField]
        ShapeBoxProperties m_Properties = new ShapeBoxProperties();

        [SerializeField]
        PivotLocation m_PivotLocation;

        ProBuilderMesh m_Mesh;

        [SerializeField]
        bool m_Edited = false;

        public Shape shape
        {
            get => m_Shape;
            set => m_Shape = value;
        }

        public PivotLocation pivotLocation
        {
            get => m_PivotLocation;
            set
            {
                m_PivotLocation = value;
                Rebuild();
            }
        }

        public Vector3 size
        {
            get => m_Shape.size;
            set => m_Shape.size = value;
        }

        public Quaternion rotation
        {
            get => m_Shape.rotation;
            set => m_Shape.rotation = value;
        }

        public bool edited
        {
            get => m_Edited;
            set => m_Edited = value;
        }

        Bounds m_EditionBounds;
        public Bounds editionBounds
        {
            get
            {
                m_EditionBounds.center = m_Shape.shapeBox.center;
                m_EditionBounds.size = m_Shape.size;
                if(Mathf.Abs(m_Shape.shapeBox.size.y) < Mathf.Epsilon)
                    m_EditionBounds.size = new Vector3(m_Shape.size.x, 0f, m_Shape.size.z);

                return m_EditionBounds;
            }
        }

        /// <summary>
        /// Reference to the <see cref="ProBuilderMesh"/> that this component is creating.
        /// </summary>
        public ProBuilderMesh mesh
        {
            get
            {
                if(m_Mesh == null)
                    m_Mesh = GetComponent<ProBuilderMesh>();
                if(m_Mesh == null)
                    m_Mesh = gameObject.AddComponent<ProBuilderMesh>();

                return m_Mesh;
            }
        }

        void UpdateProperties()
        {
            m_Properties.m_Width = size.x;
            m_Properties.m_Height = size.y;
            m_Properties.m_Length = size.z;
        }

        public void UpdateComponent()
        {
            //Recenter shape
            m_Shape.ResetPivot(mesh);
            size = new Vector3(m_Properties.m_Width, m_Properties.m_Height, m_Properties.m_Length);
            Rebuild();
        }

        public void Rebuild(Bounds bounds, Quaternion rotation)
        {
            size = bounds.size;
            transform.position = bounds.center;
            transform.rotation = rotation;

            Rebuild();
        }

        public void Rebuild()
        {
            if(gameObject == null || gameObject.hideFlags != HideFlags.None)
            {
                UpdateProperties();
                return;
            }

            m_Shape.RebuildMesh(mesh, size, rotation);
            m_Edited = false;

            Bounds bounds = m_Shape.shapeBox;
            bounds.size = Math.Abs(m_Shape.shapeBox.size);
            MeshUtility.FitToSize(mesh, bounds, size);

            m_Shape.UpdatePivot(mesh, pivotLocation);

            UpdateProperties();
        }

        public void SetShape(Shape shape)
        {
            m_Shape = shape;
            if(m_Shape is Plane || m_Shape is Sprite)
            {
                Bounds bounds = m_Shape.shapeBox;
                var newCenter = bounds.center;
                var newSize = bounds.size;
                newCenter.y = 0;
                newSize.y = 0;
                bounds.center = newCenter;
                bounds.size = newSize;
                m_Shape.shapeBox = bounds;
            }
            //Else if coming from a 2D-state and being back to a 3D shape
            //No changes is pivot is centered
            else if(pivotLocation == PivotLocation.FirstVertex
                    && m_Shape.shapeBox.size.y == 0 && size.y != 0)
            {
                Bounds bounds = m_Shape.shapeBox;
                var newCenter = bounds.center;
                var newSize = bounds.size;
                newCenter.y += size.y / 2f;
                newSize.y = size.y;
                bounds.center = newCenter;
                bounds.size = newSize;
                m_Shape.shapeBox = bounds;
            }
            m_Shape.ResetPivot(mesh);
            Rebuild();
        }

        /// <summary>
        /// Rotates the Shape by a given quaternion while respecting the bounds
        /// </summary>
        /// <param name="rotation">The angles to rotate by</param>
        public void RotateInsideBounds(Quaternion deltaRotation)
        {
            m_Shape.ResetPivot(mesh);
            rotation = deltaRotation * rotation;
            Rebuild();
        }
    }
}
