package com.example.aurorascreensaver

import android.content.Context
import android.widget.FrameLayout

class AuroraSceneView(context: Context) : FrameLayout(context) {

    val glSurfaceView: AuroraGLSurfaceView
    private val hudView: AuroraHudView

    init {
        keepScreenOn = true

        glSurfaceView = AuroraGLSurfaceView(context)
        hudView = AuroraHudView(context)

        addView(
            glSurfaceView,
            LayoutParams(
                LayoutParams.MATCH_PARENT,
                LayoutParams.MATCH_PARENT
            )
        )

        addView(
            hudView,
            LayoutParams(
                LayoutParams.MATCH_PARENT,
                LayoutParams.MATCH_PARENT
            )
        )
    }
}
