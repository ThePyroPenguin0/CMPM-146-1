using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class SteeringBehavior : MonoBehaviour
{
    public Vector3 target;
    public KinematicBehavior kinematic;
    public List<Vector3> path;
    // you can use this label to show debug information,
    // like the distance to the (next) target
    public TextMeshProUGUI label;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        kinematic = GetComponent<KinematicBehavior>();
        target = transform.position;
        path = null;
        EventBus.OnSetMap += SetMap;
    }

    // Update is called once per frame
    void Update()
    {
        // Assignment 1: If a single target was set, move to that target
        //                If a path was set, follow that path ("tightly")

        // you can use kinematic.SetDesiredSpeed(...) and kinematic.SetDesiredRotationalVelocity(...)
        //    to "request" acceleration/decceleration to a target speed/rotational velocity

        // Given in lecture slides
        Vector3 direction = target - transform.position;
        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);

        // figure out the desired speed and rotational velocity
        float dist = direction.magnitude;
        // label.text = dist.ToString() + " ";

        // trying to get the car to have a tighter turn radius when close to the target
        // and to slow down when close to the target
        if (dist <= 3) kinematic.SetDesiredSpeed(0);
        else if (dist < 25 && dist > 3) kinematic.SetDesiredSpeed(kinematic.GetMaxSpeed() * (dist / 25));
        else kinematic.SetDesiredSpeed(kinematic.GetMaxSpeed());

        if(angle < 22.5f && angle > -22.5f) kinematic.SetDesiredRotationalVelocity(0);
        else kinematic.SetDesiredRotationalVelocity(angle * 10);
        



        if (path != null)
        {
            if ((target - transform.position).magnitude < 5)
            {
                if (path.Count > 1)
                {
                    path.RemoveAt(0);
                    this.target = path[0];
                }
                else if (path.Count == 1)
                {
                    this.path = null;
                }
            }
        }
    }

    public void SetTarget(Vector3 target)
    {
        this.target = target;
        EventBus.ShowTarget(target);
    }

    public void SetPath(List<Vector3> path)
    {
        this.path = path;
        if (this.path != null)
        {
            this.target = path[0]; // This breaks the target placement
        }
    }

    public void SetMap(List<Wall> outline)
    {
        this.path = null;
        this.target = transform.position;
    }
}