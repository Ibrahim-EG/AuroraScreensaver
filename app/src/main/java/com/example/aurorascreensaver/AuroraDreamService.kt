package com.example.aurorascreensaver

import android.service.dreams.DreamService

class AuroraDreamService : DreamService() {

    private var glView: AuroraGLSurfaceView? = null

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()

        isFullscreen = true
        isInteractive = false

        val view = AuroraGLSurfaceView(this)
        glView = view
        setContentView(view)
    }

    override fun onDreamingStarted() {
        super.onDreamingStarted()
        glView?.onResume()
    }

    override fun onDreamingStopped() {
        glView?.onPause()
        super.onDreamingStopped()
    }

    override fun onDetachedFromWindow() {
        glView?.onPause()
        super.onDetachedFromWindow()
    }
}
