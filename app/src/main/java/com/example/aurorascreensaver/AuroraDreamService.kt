package com.example.aurorascreensaver

import android.service.dreams.DreamService
import android.view.WindowManager

class AuroraDreamService : DreamService() {

    private var sceneView: AuroraSceneView? = null

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()

        isFullscreen = true
        isInteractive = false

        window?.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        val view = AuroraSceneView(this)
        sceneView = view
        setContentView(view)
    }

    override fun onDreamingStarted() {
        super.onDreamingStarted()
        sceneView?.glSurfaceView?.onResume()
    }

    override fun onDreamingStopped() {
        sceneView?.glSurfaceView?.onPause()
        super.onDreamingStopped()
    }

    override fun onDetachedFromWindow() {
        sceneView?.glSurfaceView?.onPause()
        super.onDetachedFromWindow()
    }
}
