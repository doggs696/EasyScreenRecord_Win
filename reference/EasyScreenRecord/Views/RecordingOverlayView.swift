import SwiftUI

struct RecordingOverlayView: View {
    let scale: CGFloat
    let edgeMargin: CGFloat
    let showSafeZone: Bool
    @State private var rotation: Double = 0

    init(scale: CGFloat, edgeMargin: CGFloat = 0.1, showSafeZone: Bool = true) {
        self.scale = scale
        self.edgeMargin = edgeMargin
        self.showSafeZone = showSafeZone
    }

    var body: some View {
        GeometryReader { geo in
            ZStack {
                // Safe zone indicator - calculated from full window size
                if showSafeZone {
                    SafeZoneIndicator(
                        windowSize: geo.size,
                        margin: edgeMargin
                    )
                }

                // High-tech scanner lines (corner brackets)
                ViewfinderFrame()
                    .stroke(
                        LinearGradient(colors: [.red, .orange], startPoint: .topLeading, endPoint: .bottomTrailing),
                        lineWidth: 2
                    )
            }
        }
        .onAppear {
            rotation = 360
        }
    }
}

// Safe zone indicator that matches the actual detection logic
struct SafeZoneIndicator: View {
    let windowSize: CGSize
    let margin: CGFloat

    var body: some View {
        // Calculate safe zone exactly as in ScreenRecorder.updateZoom()
        let marginW = windowSize.width * margin
        let marginH = windowSize.height * margin

        let safeWidth = windowSize.width - marginW * 2
        let safeHeight = windowSize.height - marginH * 2

        // Safe zone border (green rectangle)
        Rectangle()
            .stroke(.green.opacity(0.5), lineWidth: 1)
            .frame(width: safeWidth, height: safeHeight)
    }
}

struct ViewfinderFrame: Shape {
    func path(in rect: CGRect) -> Path {
        var path = Path()
        let size: CGFloat = 20

        // Top Left
        path.move(to: CGPoint(x: 0, y: size))
        path.addLine(to: CGPoint(x: 0, y: 0))
        path.addLine(to: CGPoint(x: size, y: 0))
        
        // Top Right
        path.move(to: CGPoint(x: rect.width - size, y: 0))
        path.addLine(to: CGPoint(x: rect.width, y: 0))
        path.addLine(to: CGPoint(x: rect.width, y: size))
        
        // Bottom Right
        path.move(to: CGPoint(x: rect.width, y: rect.height - size))
        path.addLine(to: CGPoint(x: rect.width, y: rect.height))
        path.addLine(to: CGPoint(x: rect.width - size, y: rect.height))
        
        // Bottom Left
        path.move(to: CGPoint(x: size, y: rect.height))
        path.addLine(to: CGPoint(x: 0, y: rect.height))
        path.addLine(to: CGPoint(x: 0, y: rect.height - size))
        
        return path
    }
}
