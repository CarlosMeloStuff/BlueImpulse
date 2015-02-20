﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GPUParticleEmitter : MonoBehaviour
{
    public static List<GPUParticleEmitter> instances = new List<GPUParticleEmitter>();

    public static void UpdateAll()
    {
        foreach (GPUParticleEmitter f in instances)
        {
            f.ActualUpdate();
        }
    }


    public enum Shape
    {
        Sphere,
        Box,
    }

    public GPUParticleWorld[] m_targets;
    public float m_emit_count = 100.0f;
    public Shape m_shape = Shape.Sphere;
    public Vector3 m_velosity_base = Vector3.zero;
    public float m_velosity_diffuse = 0.5f;
    CSParticle[] m_tmp_to_add;
    float m_delta;

    void OnEnable()
    {
        instances.Add(this);
    }

    void OnDisable()
    {
        instances.Remove(this);
    }

    void Start()
    {
    }
    

    static float R(float r=0.5f)
    {
        return Random.Range(-r, r);
    }

    void EachTargets(System.Action<GPUParticleWorld> a)
    {
        if (m_targets.Length == 0) { GPUParticleWorld.GetInstances().ForEach(a); }
        else { foreach (var t in m_targets) { a(t); } }
    }

    void ActualUpdate()
    {
        m_delta += Time.deltaTime * m_emit_count;
        int n = (int)m_delta;
        m_delta -= n;
        if (n == 0) return;

        if(m_tmp_to_add==null || m_tmp_to_add.Length!=n)
        {
            m_tmp_to_add = new CSParticle[n];
        }

        Vector3 pos = transform.position;
        if (m_shape == Shape.Sphere)
        {
            float s = transform.localScale.x;
            for (int i = 0; i < m_tmp_to_add.Length; ++i)
            {
                m_tmp_to_add[i].position = pos + (new Vector3(R(), R(), R())).normalized * R(s * 0.5f);
                m_tmp_to_add[i].velocity = m_velosity_base + new Vector3(R(), R(), R()) * m_velosity_diffuse;
            }
        }
        else if (m_shape == Shape.Box)
        {
            Vector3 s = transform.localScale;
            for (int i = 0; i < m_tmp_to_add.Length; ++i)
            {
                m_tmp_to_add[i].position = pos + new Vector3(R(s.x), R(s.y), R(s.z));
                m_tmp_to_add[i].velocity = m_velosity_base + new Vector3(R(), R(), R()) * m_velosity_diffuse;
            }
        }
        EachTargets((t) => { t.AddParticles(m_tmp_to_add); });
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        switch (m_shape)
        {
            case Shape.Sphere:
                Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
                break;

            case Shape.Box:
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                break;
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
