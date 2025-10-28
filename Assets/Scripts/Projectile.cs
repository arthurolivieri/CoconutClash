using UnityEngine;

public class Projectile : MonoBehaviour
{
    // velocidade atual (vx, vy)
    private Vector2 velocity;

    // "gravidade" que puxa pra baixo
    private float gravityStrength;

    // só pra visual girar
    private float rotationSpeed;

    private void Update()
    {
        float dt = Time.deltaTime;

        // aplica gravidade manual (faz a parábola SEMPRE existir)
        velocity += new Vector2(0f, -gravityStrength) * dt;

        // atualiza posição com a velocidade atual
        Vector3 newPos = transform.position + (Vector3)(velocity * dt);

        // trava no plano 2D
        newPos.z = 0f;

        transform.position = newPos;

        // rotação visual (puramente cosmética)
        transform.Rotate(Vector3.forward, rotationSpeed * dt);
    }

    // chamado pelo Shooter
    public void Initialize(
        Vector2 initialVelocity,
        float gravityStrength,
        float rotationSpeed
    )
    {
        this.velocity = initialVelocity;
        this.gravityStrength = gravityStrength;
        this.rotationSpeed = rotationSpeed;
    }
}
