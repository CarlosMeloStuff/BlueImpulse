﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif



[RequireComponent(typeof(GPUParticleWorld))]
public class GPUParticleTrailRenderer : MonoBehaviour
{
    public Camera[] m_camera;
    public int m_trail_max_history = 32;
    public float m_width = 0.1f;
    public ComputeShader m_cs_trail;
    public Material m_mat_trail;

    ComputeBuffer m_buf_trail_params;
    ComputeBuffer m_buf_trail_entities;
    ComputeBuffer m_buf_trail_history;
    ComputeBuffer m_buf_trail_vertices;

    GPUParticleWorld m_pw;
    CSTrailParams[] m_tmp_params;
    System.Action m_act_render;
    int m_max_entities;
    bool m_first = true;

    const int BLOCK_SIZE = 512;

#if UNITY_EDITOR
    void Reset()
    {
        m_cs_trail = AssetDatabase.LoadAssetAtPath("Assets/GPUParticle/Shaders/Trail.compute", typeof(ComputeShader)) as ComputeShader;
        m_mat_trail = AssetDatabase.LoadAssetAtPath("Assets/GPUParticle/Materials/Trail.mat", typeof(Material)) as Material;
    }
#endif // UNITY_EDITOR


    void OnEnable()
    {
        m_pw = GetComponent<GPUParticleWorld>();
        if (m_camera == null || m_camera.Length == 0)
        {
            m_camera = new Camera[1] { Camera.main };
        }
        m_tmp_params = new CSTrailParams[1];

        m_max_entities = m_pw.GetNumMaxParticles() * 2;
        m_buf_trail_params = new ComputeBuffer(1, CSTrailParams.size);
        m_buf_trail_entities = new ComputeBuffer(m_max_entities, CSTrailEntity.size);
        m_buf_trail_history = new ComputeBuffer(m_max_entities * m_trail_max_history, CSTrailHistory.size);
        m_buf_trail_vertices = new ComputeBuffer(m_max_entities * m_trail_max_history * 2, CSTrailVertex.size);

        if (m_act_render==null)
        {
            m_act_render = Render;
            foreach (var c in m_camera)
            {
                if (c == null) continue;
                DSRenderer dsr = c.GetComponent<DSRenderer>();
                dsr.AddCallbackTransparent(m_act_render);
            }
        }
    }

    void OnDisable()
    {
        m_buf_trail_vertices.Release();
        m_buf_trail_history.Release();
        m_buf_trail_entities.Release();
        m_buf_trail_params.Release();
    }

    void LateUpdate()
    {
        if (m_first)
        {
            m_first = false;
            DispatchTrailKernel(0);
        }
        DispatchTrailKernel(1);
    }

    void DispatchTrailKernel(int i)
    {
        if (!enabled || !m_pw.enabled || Time.deltaTime == 0.0f) return;

        m_tmp_params[0].delta_time = Time.deltaTime;
        m_tmp_params[0].max_entities = m_max_entities;
        m_tmp_params[0].max_history = m_trail_max_history;
        m_tmp_params[0].camera_position = Camera.current != null ? Camera.current.transform.position : Vector3.zero;
        m_tmp_params[0].width = m_width;
        m_buf_trail_params.SetData(m_tmp_params);

        m_cs_trail.SetBuffer(i, "particles", m_pw.GetParticleBuffer());
        m_cs_trail.SetBuffer(i, "params", m_buf_trail_params);
        m_cs_trail.SetBuffer(i, "entities", m_buf_trail_entities);
        m_cs_trail.SetBuffer(i, "history", m_buf_trail_history);
        m_cs_trail.SetBuffer(i, "vertices", m_buf_trail_vertices);
        m_cs_trail.Dispatch(i, m_pw.m_max_particles/BLOCK_SIZE, 1, 1);
    }

    void Render()
    {
        if (!enabled || !m_pw.enabled || m_mat_trail == null) return;

        m_mat_trail.SetBuffer("particles", m_pw.GetParticleBuffer());
        m_mat_trail.SetBuffer("params", m_buf_trail_params);
        m_mat_trail.SetBuffer("vertices", m_buf_trail_vertices);
        m_mat_trail.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, (m_trail_max_history - 1) * 6, m_pw.GetNumMaxParticles());
    }
}
