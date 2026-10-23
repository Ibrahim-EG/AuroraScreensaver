package com.example.aurorascreensaver

import android.content.Context
import android.opengl.GLSurfaceView

class AuroraGLSurfaceView(context: Context) : GLSurfaceView(context) {

    init {
        setEGLContextClientVersion(3)
        setEGLConfigChooser(8, 8, 8, 8, 16, 0)
        setPreserveEGLContextOnPause(true)

        val appContext = context.applicationContext ?: context
        setRenderer(AuroraRenderer(appContext))

        renderMode = GLSurfaceView.RENDERMODE_CONTINUOUSLY
    }
}
