﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParticleForce : MonoBehaviour
{
	public static List<ParticleForce> instances = new List<ParticleForce>();

	public enum MPForceShape
	{
		All,
		Sphere,
		Box
	}

	public enum MPForceDirection
	{
		Directional,
		Radial,
	}
	public MPForceShape regionType;
	public MPForceDirection directionType;
	public float strengthNear = 10.0f;
	public float strengthFar = 0.0f;
	public float rangeInner = 0.0f;
	public float rangeOuter = 100.0f;
	public float attenuationExp = 0.5f;
	public Vector3 direction = new Vector3(0.0f, -1.0f, 0.0f);

	
	void OnEnable()
	{
		instances.Add(this);
	}

	void OnDisable()
	{
		instances.Remove(this);
	}

	void OnDrawGizmos()
	{
		{
			float arrowHeadAngle = 30.0f;
			float arrowHeadLength = 0.5f;
			Vector3 pos = transform.position;
			Vector3 dir = direction * strengthNear * 0.5f;

			Gizmos.matrix = Matrix4x4.identity;
			Gizmos.color = Color.yellow;
			Gizmos.DrawRay(pos, dir);

			Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1);
			Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1);
			Gizmos.DrawRay(pos + dir, right * arrowHeadLength);
			Gizmos.DrawRay(pos + dir, left * arrowHeadLength);
		}
		{
			Gizmos.color = Color.yellow;
			Gizmos.matrix = transform.localToWorldMatrix;
			switch (regionType)
			{
				case MPForceShape.Sphere:
					Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
					break;

				case MPForceShape.Box:
					Gizmos.color = Color.yellow;
					Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
					break;
			}
			Gizmos.matrix = Matrix4x4.identity;
		}
	}
}
