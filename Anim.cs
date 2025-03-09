using bella;
using System.Numerics;

namespace oomer_imgui_learn3;

public class Anim {
    public int _totalFrames = 10;
    public int _currentFrame = 0;
    public bella.Mat4[] _camAnim = new bella.Mat4[2];
    public double[] _focusDistAnim = new double[2];


    public ( bella.Mat4, float ) slerpCamera( float time ) {
            float timeVal = time;
                    // start end vars
            System.Numerics.Vector2 focus0;
            System.Numerics.Vector2 focus1;
            System.Numerics.Vector3 ricsPos0;
            System.Numerics.Vector3 ricsPos1;
            System.Numerics.Quaternion ricsQuat0;
            System.Numerics.Quaternion ricsQuat1;

            // Convert bella.Mat4 to System.Numerics
            System.Numerics.Matrix4x4 _rotStartMatrix = new System.Numerics.Matrix4x4(  (float) _camAnim[0].m00,
                                                                                        (float) _camAnim[0].m01,
                                                                                        (float) _camAnim[0].m02,
                                                                                        (float) _camAnim[0].m03,
                                                                                        (float) _camAnim[0].m10,
                                                                                        (float) _camAnim[0].m11,
                                                                                        (float) _camAnim[0].m12,
                                                                                        (float) _camAnim[0].m13,
                                                                                        (float) _camAnim[0].m20,
                                                                                        (float) _camAnim[0].m21,
                                                                                        (float) _camAnim[0].m22,
                                                                                        (float) _camAnim[0].m23,
                                                                                        (float) _camAnim[0].m30,
                                                                                        (float) _camAnim[0].m31,
                                                                                        (float) _camAnim[0].m32,
                                                                                        (float) _camAnim[0].m33);

            System.Numerics.Matrix4x4 _rotEndMatrix = new System.Numerics.Matrix4x4(    (float) _camAnim[1].m00,
                                                                                        (float) _camAnim[1].m01,
                                                                                        (float) _camAnim[1].m02,
                                                                                        (float) _camAnim[1].m03,
                                                                                        (float) _camAnim[1].m10,
                                                                                        (float) _camAnim[1].m11,
                                                                                        (float) _camAnim[1].m12,
                                                                                        (float) _camAnim[1].m13,
                                                                                        (float) _camAnim[1].m20,
                                                                                        (float) _camAnim[1].m21,
                                                                                        (float) _camAnim[1].m22,
                                                                                        (float) _camAnim[1].m23,
                                                                                        (float) _camAnim[1].m30,
                                                                                        (float) _camAnim[1].m31,
                                                                                        (float) _camAnim[1].m32,
                                                                                        (float) _camAnim[1].m33);

            // NEW normalize start
            System.Numerics.Matrix4x4 _normRotStartMatrix = NormalizeRotationMatrix( _rotStartMatrix );
            System.Numerics.Matrix4x4 _normRotEndMatrix = NormalizeRotationMatrix( _rotEndMatrix );
            
            // Create Quaternions needed for animation
            ricsQuat0 = System.Numerics.Quaternion.CreateFromRotationMatrix( _normRotStartMatrix );
            ricsQuat1 = System.Numerics.Quaternion.CreateFromRotationMatrix( _normRotEndMatrix );

            // Get Position Vector3
            ricsPos0 = new System.Numerics.Vector3( (float)_camAnim[0].v3.x,
                                                    (float)_camAnim[0].v3.y, 
                                                    (float)_camAnim[0].v3.z );
            ricsPos1 = new System.Numerics.Vector3( (float)_camAnim[1].v3.x, 
                                                    (float)_camAnim[1].v3.y,
                                                    (float)_camAnim[1].v3.z );
            

            // Get keyframes for FocusDist
            focus0 = new Vector2( (float)_focusDistAnim[0] , (float)0);
            focus1 = new Vector2( (float)_focusDistAnim[1] , (float)0);

            // Interpolate new FocusDist for timeVal 0-1
            Vector2 lerpFocus = System.Numerics.Vector2.Lerp( focus0, focus1, timeVal);

            // Interpolate quaternion from start quat to end quat
            var slerpedQuat = System.Numerics.Quaternion.Slerp( ricsQuat0, ricsQuat1, timeVal);

            // lerp between start pos and end pos
            var lerpedPos = System.Numerics.Vector3.Lerp( ricsPos0, ricsPos1, timeVal);
            var slerpedBellaQuat = new bella.Quat( slerpedQuat.X, slerpedQuat.Y, slerpedQuat.Z, slerpedQuat.W);
            
            var animMat4 = new bella.Mat4( new bella.Pos3( lerpedPos.X, lerpedPos.Y, lerpedPos.Z),
                                         slerpedBellaQuat,
                                         _camAnim[0].scale());
            return (animMat4, lerpFocus.X);
    }

    // Normalize rotation matrix
    // A normalized matrix will have the column vectors ranged 0-1
    public Matrix4x4 NormalizeRotationMatrix(Matrix4x4 matrix) {
        // Normalize each column vector of the rotation part
        Vector3 col1 = System.Numerics.Vector3.Normalize(new Vector3(matrix.M11, matrix.M21, matrix.M31));
        Vector3 col2 = System.Numerics.Vector3.Normalize(new Vector3(matrix.M12, matrix.M22, matrix.M32));
        Vector3 col3 = System.Numerics.Vector3.Normalize(new Vector3(matrix.M13, matrix.M23, matrix.M33));
        // Create a new rotation matrix with normalized column vectors
        Matrix4x4 normalizedRotationMatrix = new Matrix4x4 (
            col1.X, col2.X, col3.X, 0,
            col1.Y, col2.Y, col3.Y, 0,
            col1.Z, col2.Z, col3.Z, 0,
            0,      0,      0,      1
        );
        return normalizedRotationMatrix;
    }

    public bella.Mat3 bellaNormalizeRotationMatrix(bella.Mat3 inmatrix) {
        double[,] matrix = {
            {inmatrix.m00, inmatrix.m01, inmatrix.m02},
            {inmatrix.m10, inmatrix.m11, inmatrix.m12},
            {inmatrix.m20, inmatrix.m21, inmatrix.m22}
        };        
        // Normalize each column vector
        for (int i = 0; i < 3; i++) {
            float length = (float)Math.Sqrt( matrix[0, i] * matrix[0, i] + 
                                             matrix[1, i] * matrix[1, i] + 
                                             matrix[2, i] * matrix[2, i]);
            for (int j = 0; j < 3; j++) {
                matrix[j, i] /= length;
            }
        }
        // Ensure orthogonality
        // You can perform Gram-Schmidt orthogonalization here if needed

        // Ensure determinant is approximately 1
        double det = matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1]) -
                    matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0]) +
                    matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);

        if (det < 0) {
            // Change sign of one element to make determinant positive
            for (int i = 0; i < 3; i++) {
                matrix[i, 0] = -matrix[i, 0];
            }
        }
        inmatrix.m00 = matrix[0,0];
        inmatrix.m01 = matrix[0,1];
        inmatrix.m02 = matrix[0,2];
        inmatrix.m10 = matrix[1,0];
        inmatrix.m11 = matrix[1,1];
        inmatrix.m12 = matrix[1,2];
        inmatrix.m20 = matrix[2,0];
        inmatrix.m21 = matrix[2,1];
        inmatrix.m22 = matrix[2,2];
        return inmatrix;
    }

    double dot(bella.Vec4 vector1, bella.Vec4 vector2) {
        double result = 0;
        result += vector1.x * vector2.x;
        result += vector1.y * vector2.y;
        result += vector1.z * vector2.z;
        result += vector1.w * vector2.w;
        return result;
    }

    public bella.Vec3 eulers( bella.Mat3 m )
    {
        Double halfPi = 1.57079632679489661923; 
        bella.Vec3 o;
        if ( m.m02 < -0.999 ) {
            o.z = 0;
            o.x = System.Math.Atan2( m.m10, m.m20 );
            o.y = halfPi;
        } else if ( m.m02 > 0.999 ) {
            o.z = 0;
            o.x = System.Math.Atan2( -m.m10, -m.m20 );
            o.y = -halfPi;
        } else {
            var ry = -System.Math.Asin( m.m02 );
            var i = 1 / System.Math.Sqrt( 1 - m.m02 * m.m02 );
            o.z = System.Math.Atan2( m.m01 * i, m.m00 * i );
            o.x = System.Math.Atan2( m.m12 * i, m.m22 * i );
            o.y = ry;
        }
        o.x *= 57.2957795131;
        o.y *= 57.2957795131;
        o.z *= 57.2957795131;
        return o;
    }



}