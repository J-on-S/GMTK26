using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
  public CharacterController controller;
 
  public float speed = 12f;
  public float gravity = -9.81f; 
 
  public Transform groundCheck;
  public float groundDistance = 0.4f;
  public LayerMask groundMask;
 
  Vector3 velocity; // of falling
  bool isGrounded;
  
  void Update(){
    //checking if we hit the ground to reset our falling velocity, 
    // otherwise we will fall faster the next time
    // It had sphere in bottom of player, and checks if layer is there
    // groundDistance is radius of that sphere
    isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

    // reseting velocity, otherwise u get falling faster and faster
    if (isGrounded && velocity.y < 0){
      velocity.y = -2f;
    }
 
    float x = Input.GetAxis("Horizontal"); // if we press a or d -> +1 or -1
    float z = Input.GetAxis("Vertical"); // if we press w or s -> +1 or -1 
    //right is the red Axis(x), foward is the blue axis (z)
    // horizontal speed
    Vector3 move = transform.right * x + transform.forward * z;

    // use Time.deltaTime to be consisted with frames
    controller.Move(move * speed * Time.deltaTime);

    // vertical speed
    velocity.y += gravity * Time.deltaTime; // acceleration -> speed
    controller.Move(velocity * Time.deltaTime); // speed -> distance
    }
}