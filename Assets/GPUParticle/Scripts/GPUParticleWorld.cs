﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif



public class GPUParticleWorld : MonoBehaviour
{
    static List<GPUParticleWorld> s_instances;
    static int s_update_count;

    public static List<GPUParticleWorld> GetInstances()
    {
        if (s_instances == null) { s_instances = new List<GPUParticleWorld>(); }
        return s_instances;
    }



    public delegate void ParticleHandler(CSParticle[] particles, int num_particles, List<GPUParticleColliderBase> colliders);

    public enum Dimension
    {
        Dimendion3D,
        Dimendion2D,
    }

    public enum Interaction
    {
        Impulse,
        SPH,
        None,
    }

    public enum RenderMode
    {
        Point,
        Billboard,
        Cube,
    }



    public Interaction m_solver = Interaction.Impulse;
    public Dimension m_dimension = Dimension.Dimendion3D;
    public int m_max_particles = 32768;
    public float m_particle_radius = 0.05f;
    public int m_world_div_x = 256;
    public int m_world_div_y = 1;
    public int m_world_div_z = 256;
    public float m_lifetime = 20.0f;
    public float m_decelerate = 0.99f;
    public float m_advection = 0.5f;
    public float m_pressure_stiffness = 500.0f;
    public float m_wall_stiffness = 1000.0f;
    public Vector3 m_coord_scaler = Vector3.one;

    public float m_sph_smoothlen = 0.2f;
    public float m_sph_particleMass = 0.0002f;
    public float m_sph_pressureStiffness = 200.0f;
    public float m_sph_restDensity = 1000.0f;
    public float m_sph_viscosity = 0.1f;

    public bool m_process_colliders = true;
    public bool m_process_forces = true;
    public bool m_process_gbuffer_collision = false;
    public bool m_writeback_to_cpu_size = false;
    public ParticleHandler handler;

    public ComputeShader m_cs_core;
    public ComputeShader m_cs_sort;
    public ComputeShader m_cs_hashgrid;

    CSParticle[] m_particles;
    CSWorldData[] m_world_data = new CSWorldData[1];
    CSWorldIData[] m_world_idata = new CSWorldIData[1];
    CSSPHParams[] m_sph_params = new CSSPHParams[1];

    List<CSParticle> m_particles_to_add = new List<CSParticle>();
    ComputeBuffer m_buf_world_data;
    ComputeBuffer m_buf_world_idata;
    ComputeBuffer m_buf_sph_params;
    ComputeBuffer m_buf_cells;
    ComputeBuffer[] m_buf_particles = new ComputeBuffer[2];
    ComputeBuffer m_buf_particles_to_add;
    ComputeBuffer m_buf_imd;
    ComputeBuffer[] m_buf_sort_data = new ComputeBuffer[2];
    GPUSort m_bitonic_sort;

    int m_max_sphere_colliders = 256;
    int m_max_capsule_colliders = 256;
    int m_max_box_colliders = 256;
    int m_max_forces = 128;
    ComputeBuffer m_buf_sphere_colliders;
    ComputeBuffer m_buf_box_colliders;
    ComputeBuffer m_buf_capsule_colliders;
    ComputeBuffer m_buf_forces;
    List<CSSphereCollider> m_sphere_colliders = new List<CSSphereCollider>();
    List<CSCapsuleCollider> m_capsule_colliders = new List<CSCapsuleCollider>();
    List<CSBoxCollider> m_box_colliders = new List<CSBoxCollider>();
    List<CSForce> m_forces = new List<CSForce>();

    CSCell[] m_dbg_cell_data;
    GPUSort.KIP[] m_dbg_sort_data;

    int kAddParticles;
    int kPrepare;
    int kProcessInteraction_Impulse;
    int kProcessInteraction_SPH_Pass1;
    int kProcessInteraction_SPH_Pass2;
    int kProcessColliders;
    int kProcessGBufferCollision;
    int kProcessForces;
    int kIntegrate;
    int kProcessInteraction_Impulse2D;
    int kProcessInteraction_SPH_Pass12D;
    int kProcessInteraction_SPH_Pass22D;

    const int BLOCK_SIZE = 512;

    public ComputeBuffer GetParticleBuffer() { return m_buf_particles[0]; }
    public int GetNumMaxParticles() { return m_max_particles; }
    //public int GetNumParticles() { return m_world_idata[0].num_active_particles; }

    public void AddParticles(CSParticle[] particles) { if(enabled) m_particles_to_add.AddRange(particles); }
    public void AddSphereCollider(ref CSSphereCollider v) { if (enabled) m_sphere_colliders.Add(v); }
    public void AddCapsuleCollider(ref CSCapsuleCollider v) { if (enabled) m_capsule_colliders.Add(v); }
    public void AddBoxCollider(ref CSBoxCollider v) { if (enabled) m_box_colliders.Add(v); }
    public void AddForce(ref CSForce v) { if (enabled) m_forces.Add(v); }

#if UNITY_EDITOR
    void Reset()
    {
        m_cs_core = AssetDatabase.LoadAssetAtPath("Assets/GPUParticle/Shaders/ParticleCore.compute", typeof(ComputeShader)) as ComputeShader;
        m_cs_sort = AssetDatabase.LoadAssetAtPath("Assets/GPUParticle/Shaders/BitonicSort.compute", typeof(ComputeShader)) as ComputeShader;
        m_cs_hashgrid = AssetDatabase.LoadAssetAtPath("Assets/GPUParticle/Shaders/HashGrid.compute", typeof(ComputeShader)) as ComputeShader;
    }
#endif // UNITY_EDITOR

    void Awake()
    {
        if (!SystemInfo.supportsComputeShaders) {
            Debug.Log("GPUParticle: compute shader is not available.");
            enabled = false;
            return;
        }
        kAddParticles = m_cs_core.FindKernel("AddParticles");
        kPrepare = m_cs_core.FindKernel("Prepare");
        kProcessInteraction_Impulse = m_cs_core.FindKernel("ProcessInteraction_Impulse");
        kProcessInteraction_SPH_Pass1 = m_cs_core.FindKernel("ProcessInteraction_SPH_Density");
        kProcessInteraction_SPH_Pass2 = m_cs_core.FindKernel("ProcessInteraction_SPH_Force");
        kProcessColliders = m_cs_core.FindKernel("ProcessColliders");
        kProcessGBufferCollision = m_cs_core.FindKernel("ProcessGBufferCollision");
        kProcessForces = m_cs_core.FindKernel("ProcessForces");
        kIntegrate = m_cs_core.FindKernel("Integrate");
        kProcessInteraction_Impulse2D = m_cs_core.FindKernel("ProcessInteraction_Impulse2D");
        kProcessInteraction_SPH_Pass12D = m_cs_core.FindKernel("ProcessInteraction_SPH_Density2D");
        kProcessInteraction_SPH_Pass22D = m_cs_core.FindKernel("ProcessInteraction_SPH_Force2D");
    }

    void OnEnable()
    {
        GetInstances().Add(this);

        m_world_data[0].SetDefaultValues();
        m_world_data[0].num_max_particles = m_max_particles;
        m_world_data[0].SetWorldSize(transform.position, transform.localScale * 0.5f,
            new UVector3 { x = (uint)m_world_div_x, y = (uint)m_world_div_y, z = (uint)m_world_div_z });
        m_sph_params[0].SetDefaultValues(m_world_data[0].particle_size);

        m_particles = new CSParticle[m_max_particles];
        for (int i = 0; i < m_particles.Length; ++i)
        {
            m_particles[i].hit_objid = -1;
            //particles[i].owner_objid = -1;
            m_particles[i].lifetime = 0.0f;
        }


        IVector3 world_div = m_world_data[0].world_div;
        int num_cells = world_div.x * world_div.y * world_div.z;

        m_buf_world_data = new ComputeBuffer(1, CSWorldData.size);
        m_buf_world_idata = new ComputeBuffer(1, CSWorldIData.size);
        m_buf_world_idata.SetData(m_world_idata);
        m_buf_sph_params = new ComputeBuffer(1, CSSPHParams.size);
        m_buf_cells = new ComputeBuffer(num_cells, CSCell.size);
        m_buf_particles[0] = new ComputeBuffer(m_max_particles, CSParticle.size);
        m_buf_particles[1] = new ComputeBuffer(m_max_particles, CSParticle.size);
        m_buf_particles[0].SetData(m_particles);
        m_buf_particles_to_add = new ComputeBuffer(m_max_particles, CSParticle.size);
        m_buf_imd = new ComputeBuffer(m_max_particles, CSParticleIData.size);
        m_buf_sort_data[0] = new ComputeBuffer(m_max_particles, CSSortData.size);
        m_buf_sort_data[1] = new ComputeBuffer(m_max_particles, CSSortData.size);

        m_bitonic_sort = new GPUSort();
        m_bitonic_sort.Initialize(m_cs_sort);

        m_buf_sphere_colliders = new ComputeBuffer(m_max_sphere_colliders, CSSphereCollider.size);
        m_buf_capsule_colliders = new ComputeBuffer(m_max_capsule_colliders, CSCapsuleCollider.size);
        m_buf_box_colliders = new ComputeBuffer(m_max_box_colliders, CSBoxCollider.size);
        m_buf_forces = new ComputeBuffer(m_max_forces, CSForce.size);
    }

    void OnDisable()
    {
        GetInstances().Remove(this);

        if (m_buf_forces != null)
        {
            m_buf_forces.Release();
            m_buf_box_colliders.Release();
            m_buf_capsule_colliders.Release();
            m_buf_sphere_colliders.Release();

            m_bitonic_sort.Release();
            m_buf_particles_to_add.Release();
            m_buf_imd.Release();
            m_buf_sort_data[0].Release();
            m_buf_sort_data[1].Release();
            m_buf_particles[0].Release();
            m_buf_particles[1].Release();
            m_buf_cells.Release();
            m_buf_sph_params.Release();
            m_buf_world_idata.Release();
            m_buf_world_data.Release();
        }
    }



    public void HandleParticleCollision()
    {
    }

    void Update()
    {
        if (s_update_count++ == 0)
        {
            GPUParticleEmitter.UpdateAll();
            GPUParticleForce.UpdateAll();
            GPUParticleColliderBase.UpdateAll();
        }

        m_world_data[0].particle_size = m_particle_radius;
        m_world_data[0].particle_lifetime = m_lifetime;
        m_world_data[0].num_sphere_colliders = m_sphere_colliders.Count;
        m_world_data[0].num_capsule_colliders = m_capsule_colliders.Count;
        m_world_data[0].num_box_colliders = m_box_colliders.Count;
        m_world_data[0].num_forces = m_forces.Count;
        m_world_data[0].decelerate = m_decelerate;
        m_world_data[0].advection = m_advection;
        m_world_data[0].coord_scaler = m_coord_scaler;
        m_world_data[0].pressure_stiffness = m_pressure_stiffness;
        m_world_data[0].wall_stiffness = m_wall_stiffness;

        m_sph_params[0].smooth_len = m_sph_smoothlen;
        m_sph_params[0].particle_mass = m_sph_particleMass;
        m_sph_params[0].pressure_stiffness = m_sph_pressureStiffness;
        m_sph_params[0].rest_density = m_sph_restDensity;
        m_sph_params[0].viscosity = m_sph_viscosity;

        m_buf_sphere_colliders.SetData(m_sphere_colliders.ToArray());   m_sphere_colliders.Clear();
        m_buf_capsule_colliders.SetData(m_capsule_colliders.ToArray()); m_capsule_colliders.Clear();
        m_buf_box_colliders.SetData(m_box_colliders.ToArray());         m_box_colliders.Clear();
        m_buf_forces.SetData(m_forces.ToArray());                       m_forces.Clear();

        IVector3 world_div = m_world_data[0].world_div;
        int num_cells = world_div.x * world_div.y * world_div.z;

        m_world_data[0].num_additional_particles = m_particles_to_add.Count;
        m_buf_world_data.SetData(m_world_data);
        CSWorldData csWorldData = m_world_data[0];
        m_buf_sph_params.SetData(m_sph_params);


        // add new particles
        if (m_particles_to_add.Count > 0)
        {
            ComputeShader cs = m_cs_core;
            int kernel = kAddParticles;
            m_buf_particles_to_add.SetData(m_particles_to_add.ToArray());
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "world_idata", m_buf_world_idata);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "particles_to_add", m_buf_particles_to_add);
            cs.Dispatch(kernel, m_particles_to_add.Count / BLOCK_SIZE + 1, 1, 1);
            m_particles_to_add.Clear();
        }

        {
            // clear cells
            {
                ComputeShader cs = m_cs_hashgrid;
                int kernel = 0;
                cs.SetBuffer(kernel, "cells_rw", m_buf_cells);
                cs.Dispatch(kernel, num_cells / BLOCK_SIZE, 1, 1);
            }
            // generate hashes
            {
                ComputeShader cs = m_cs_hashgrid;
                int kernel = 1;
                cs.SetBuffer(kernel, "world_data", m_buf_world_data);
                cs.SetBuffer(kernel, "world_idata", m_buf_world_idata);
                cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
                cs.SetBuffer(kernel, "sort_keys_rw", m_buf_sort_data[0]);
                cs.Dispatch(kernel, m_max_particles / BLOCK_SIZE, 1, 1);
            }
            // sort keys
            {
                m_bitonic_sort.BitonicSort(m_buf_sort_data[0], m_buf_sort_data[1], (uint)csWorldData.num_max_particles);
            }
            // reorder particles
            {
                ComputeShader cs = m_cs_hashgrid;
                int kernel = 2;
                cs.SetBuffer(kernel, "world_data", m_buf_world_data);
                cs.SetBuffer(kernel, "world_idata", m_buf_world_idata);
                cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
                cs.SetBuffer(kernel, "particles_rw", m_buf_particles[1]);
                cs.SetBuffer(kernel, "sort_keys", m_buf_sort_data[0]);
                cs.SetBuffer(kernel, "cells_rw", m_buf_cells);
                cs.Dispatch(kernel, m_max_particles / BLOCK_SIZE, 1, 1);
                GPUParticleUtils.Swap(ref m_buf_particles[0], ref m_buf_particles[1]);
            }
        }


        //{
        //	dbgSortData = new GPUSort.KIP[csWorldData.num_max_particles];
        //	cbSortData[0].GetData(dbgSortData);
        //	uint prev = 0;
        //	for (int i = 0; i < dbgSortData.Length; ++i)
        //	{
        //		if (prev > dbgSortData[i].key)
        //		{
        //			Debug.Log("sort bug: "+i);
        //			break;
        //		}
        //		prev = dbgSortData[i].key;
        //	}
        //}

        //dbgCellData = new CellData[num_cells];
        //cbCells.GetData(dbgCellData);
        //for (int i = 0; i < num_cells; ++i )
        //{
        //	if (dbgCellData[i].begin!=0)
        //	{
        //		Debug.Log("dbgCellData:" + dbgCellData[i].begin + "," + dbgCellData[i].end);
        //		break;
        //	}
        //}

        int num_active_blocks = m_max_particles / BLOCK_SIZE;

        // initialize intermediate data
        {
            ComputeShader cs = m_cs_core;
            int kernel = kPrepare;
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "pimd", m_buf_imd);
            cs.SetBuffer(kernel, "cells", m_buf_cells);
            cs.Dispatch(kernel, num_active_blocks, 1, 1);
        }

        // particle interaction
        if (m_solver == GPUParticleWorld.Interaction.Impulse)
        {
            ComputeShader cs = m_cs_core;
            int kernel = m_dimension == GPUParticleWorld.Dimension.Dimendion3D ?
                kProcessInteraction_Impulse : kProcessInteraction_Impulse2D;
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "pimd", m_buf_imd);
            cs.SetBuffer(kernel, "cells", m_buf_cells);
            cs.Dispatch(kernel, num_active_blocks, 1, 1);
        }
        else if (m_solver == GPUParticleWorld.Interaction.SPH)
        {
            ComputeShader cs = m_cs_core;
            int kernel = m_dimension == GPUParticleWorld.Dimension.Dimendion3D ?
                kProcessInteraction_SPH_Pass1 : kProcessInteraction_SPH_Pass12D;
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "sph_params", m_buf_sph_params);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "pimd", m_buf_imd);
            cs.SetBuffer(kernel, "cells", m_buf_cells);
            cs.Dispatch(kernel, num_active_blocks, 1, 1);

            kernel = m_dimension == GPUParticleWorld.Dimension.Dimendion3D ?
                kProcessInteraction_SPH_Pass2 : kProcessInteraction_SPH_Pass22D;
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "sph_params", m_buf_sph_params);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "pimd", m_buf_imd);
            cs.SetBuffer(kernel, "cells", m_buf_cells);
            cs.Dispatch(kernel, num_active_blocks, 1, 1);
        }
        else if (m_solver == GPUParticleWorld.Interaction.None)
        {
            // do nothing
        }

        //// gbuffer collision
        //if (m_process_gbuffer_collision)
        //{
        //    ComputeShader cs = m_cs_core;
        //    int kernel = kProcessGBufferCollision;
        //    cs.SetTexture(kernel, "gbuffer_normal", rtNormalBufferCopy);
        //    cs.SetTexture(kernel, "gbuffer_position", rtPositionBufferCopy);
        //    cs.SetBuffer(kernel, "world_data", m_buf_world_data);
        //    cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
        //    cs.SetBuffer(kernel, "pimd", m_buf_imd);
        //    cs.Dispatch(kernel, num_active_blocks, 1, 1);
        //}

        // colliders
        if (m_process_colliders)
        {
            ComputeShader cs = m_cs_core;
            int kernel = kProcessColliders;
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "pimd", m_buf_imd);
            cs.SetBuffer(kernel, "cells", m_buf_cells);
            cs.SetBuffer(kernel, "sphere_colliders", m_buf_sphere_colliders);
            cs.SetBuffer(kernel, "capsule_colliders", m_buf_capsule_colliders);
            cs.SetBuffer(kernel, "box_colliders", m_buf_box_colliders);
            cs.Dispatch(kernel, num_active_blocks, 1, 1);
        }

        // forces
        if (m_process_forces)
        {
            ComputeShader cs = m_cs_core;
            int kernel = kProcessForces;
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "pimd", m_buf_imd);
            cs.SetBuffer(kernel, "cells", m_buf_cells);
            cs.SetBuffer(kernel, "forces", m_buf_forces);
            cs.Dispatch(kernel, num_active_blocks, 1, 1);
        }

        // integrate
        {
            ComputeShader cs = m_cs_core;
            int kernel = kIntegrate;
            cs.SetBuffer(kernel, "world_data", m_buf_world_data);
            cs.SetBuffer(kernel, "particles", m_buf_particles[0]);
            cs.SetBuffer(kernel, "pimd", m_buf_imd);
            cs.Dispatch(kernel, num_active_blocks, 1, 1);
        }
    }

    void LateUpdate()
    {
        --s_update_count;
    }


    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }

}
