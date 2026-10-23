package com.example.aurorascreensaver

import android.content.Context
import android.opengl.GLES30
import android.opengl.GLSurfaceView
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.FloatBuffer
import javax.microedition.khronos.egl.EGLConfig
import javax.microedition.khronos.opengles.GL10

class AuroraRenderer(private val context: Context) : GLSurfaceView.Renderer {

    private var program = 0
    private var vao = 0
    private var vbo = 0
    private var positionLocation = 0
    private var resolutionLocation = 0
    private var timeLocation = 0

    private var startTime = System.nanoTime()

    private var width = 1
    private var height = 1

    private lateinit var positionBuffer: FloatBuffer

    override fun onSurfaceCreated(gl: GL10?, config: EGLConfig?) {
        GLES30.glClearColor(0f, 0f, 0f, 1f)

        program = ShaderResources.createProgram(
            context,
            "shaders/fullscreen.vert",
            "shaders/aurora.frag"
        )

        positionLocation = GLES30.glGetAttribLocation(program, "aPosition")
        resolutionLocation = GLES30.glGetUniformLocation(program, "uResolution")
        timeLocation = GLES30.glGetUniformLocation(program, "uTime")

        val positions = floatArrayOf(
            -1f, -1f,
            3f, -1f,
            -1f, 3f
        )

        positionBuffer = ByteBuffer.allocateDirect(positions.size * 4)
            .order(ByteOrder.nativeOrder())
            .asFloatBuffer()

        positionBuffer.put(positions)
        positionBuffer.position(0)

        val vaos = IntArray(1)
        GLES30.glGenVertexArrays(1, vaos, 0)
        vao = vaos[0]

        GLES30.glBindVertexArray(vao)

        val buffers = IntArray(1)
        GLES30.glGenBuffers(1, buffers, 0)
        vbo = buffers[0]

        GLES30.glBindBuffer(GLES30.GL_ARRAY_BUFFER, vbo)
        GLES30.glBufferData(
            GLES30.GL_ARRAY_BUFFER,
            positions.size * 4,
            positionBuffer,
            GLES30.GL_STATIC_DRAW
        )

        GLES30.glVertexAttribPointer(
            positionLocation,
            2,
            GLES30.GL_FLOAT,
            false,
            0,
            0
        )

        GLES30.glEnableVertexAttribArray(positionLocation)

        GLES30.glBindVertexArray(0)
    }

    override fun onSurfaceChanged(gl: GL10?, w: Int, h: Int) {
        width = w
        height = h
        GLES30.glViewport(0, 0, w, h)
    }

    override fun onDrawFrame(gl: GL10?) {
        GLES30.glClear(GLES30.GL_COLOR_BUFFER_BIT)

        if (program == 0) {
            return
        }

        val time = (System.nanoTime() - startTime) / 1_000_000_000f

        GLES30.glUseProgram(program)

        GLES30.glUniform3f(
            resolutionLocation,
            width.toFloat(),
            height.toFloat(),
            0f
        )

        GLES30.glUniform1f(timeLocation, time)

        GLES30.glBindVertexArray(vao)
        GLES30.glDrawArrays(GLES30.GL_TRIANGLES, 0, 3)
        GLES30.glBindVertexArray(0)
    }
}
