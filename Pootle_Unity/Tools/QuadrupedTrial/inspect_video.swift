import Foundation
import AVFoundation
let paths = CommandLine.arguments.dropFirst()
for path in paths {
    let asset = AVURLAsset(url: URL(fileURLWithPath: path))
    let duration = try await asset.load(.duration)
    let tracks = try await asset.loadTracks(withMediaType: .video)
    let track = tracks[0]
    let size = try await track.load(.naturalSize)
    let rate = try await track.load(.nominalFrameRate)
    let reader = try AVAssetReader(asset: asset)
    let output = AVAssetReaderTrackOutput(track: track, outputSettings: nil)
    reader.add(output);reader.startReading()
    var count=0
    while output.copyNextSampleBuffer() != nil {count+=1}
    let data:[String:Any] = ["file":path,"duration":duration.seconds,"width":size.width,"height":size.height,"fps":rate,"frames":count,"readCompleted":reader.status == .completed]
    let json=try JSONSerialization.data(withJSONObject:data,options:[.sortedKeys])
    print(String(data:json,encoding:.utf8)!)
}
