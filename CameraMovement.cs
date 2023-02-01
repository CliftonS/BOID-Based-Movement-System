using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{

    float speed = 8.0f;
    float zoomSpeed = 1000f;
    float rotationSpeed = 0.4f;

    float maxHeight = 40f;
    float minHeight = 2f;

    Vector2 p1;
    Vector3 p2;

    public Transform selectionAreaTransform;

    // Update is called once per frame
    void Update()
    {
        /*if (Input.GetKey(KeyCode.LeftShift))
        {
            speed = 0.06f;
            zoomSpeed = 20f;
        } else
        {
            speed = 0.035f;
            zoomSpeed = 10f;
        }*/

        float hsp = speed * Input.GetAxis("Horizontal") * Time.deltaTime;
        float vsp = speed * Input.GetAxis("Vertical") * Time.deltaTime;
        float scrollSp = Mathf.Log(transform.position.y) * -zoomSpeed * Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime;
        if (float.IsNaN(scrollSp))
        {
            scrollSp = 0;
        }

        if ((transform.transform.position.y >= maxHeight) && scrollSp > 0)
        {
            scrollSp = 0;
        }
        else if ((transform.position.y <= minHeight) && scrollSp < 0)
        {
            scrollSp = 0;
        }

        if (transform.position.y + scrollSp > maxHeight)
        {
            scrollSp = transform.position.y - maxHeight;
        }
        else if (transform.position.y + scrollSp < minHeight)
        {
            scrollSp = minHeight - transform.position.y;
        }

        Vector3 verticalMove = new Vector3(0, scrollSp, 0);
        Vector3 lateralMove = hsp * transform.right;
        Vector3 forwardMove = transform.forward;
        forwardMove.y = 0;
        forwardMove.Normalize();
        forwardMove *= vsp;

        Vector3 move = verticalMove + lateralMove + forwardMove;

        transform.position += move;

        getCameraRotation();
    }

    void getCameraRotation()
    {
        if (Input.GetMouseButtonDown(2))
        {
            p1 = Input.mousePosition;
        }

        if (Input.GetMouseButton(2))
        {
            p2 = Input.mousePosition;

            float dx = (p2 - (Vector3)p1).x * rotationSpeed * Time.deltaTime;
            float dy = (p2 - (Vector3)p1).y * rotationSpeed;

            transform.rotation *= Quaternion.Euler(new Vector3(0, dx, 0));
        }
    }
}
