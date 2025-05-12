// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(document).ready(function() {
    // Update duration display value
    $('#shortsDuration').on('input', function() {
        $('#durationValue').text($(this).val() + 's');
    });

    // Transcription form
    $('#transcriptionForm').on('submit', function(e) {
        e.preventDefault();
        const youtubeUrl = $('#youtubeUrl').val();
        
        if (!youtubeUrl) {
            showError("Please enter a YouTube URL");
            return;
        }
        
        showLoading('#transcribeBtn', '#btnSpinner');
        
        // Call API to transcribe video
        $.ajax({
            url: '/api/Transcription/transcribe',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ youtubeUrl: youtubeUrl }),
            success: function(response) {
                hideLoading('#transcribeBtn', '#btnSpinner');
                if (response.success) {
                    showTranscriptionResult(response.text);
                } else {
                    showError(response.errorMessage || "Failed to transcribe video");
                }
            },
            error: function(xhr) {
                hideLoading('#transcribeBtn', '#btnSpinner');
                showError(xhr.responseJSON?.errorMessage || "Failed to transcribe video. Please try again later.");
            }
        });
    });
    
    // Best moments form
    $('#bestMomentsForm').on('submit', function(e) {
        e.preventDefault();
        const youtubeUrl = $('#bestMomentsYoutubeUrl').val();
        
        if (!youtubeUrl) {
            showError("Please enter a YouTube URL");
            return;
        }
        
        showLoading('#findBestMomentsBtn', '#bestMomentsSpinner');
        
        // Call API to find best moments
        $.ajax({
            url: '/api/Transcription/transcribe-and-extract',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ youtubeUrl: youtubeUrl }),
            success: function(response) {
                hideLoading('#findBestMomentsBtn', '#bestMomentsSpinner');
                if (response.success) {
                    showBestMomentsResult(response.bestMoments, response.transcription);
                } else {
                    showError(response.errorMessage || "Failed to find best moments");
                }
            },
            error: function(xhr) {
                hideLoading('#findBestMomentsBtn', '#bestMomentsSpinner');
                showError(xhr.responseJSON?.errorMessage || "Failed to find best moments. Please try again later.");
            }
        });
    });
    
    // Shorts creation form
    $('#shortsForm').on('submit', function(e) {
        e.preventDefault();
        const youtubeUrl = $('#shortsYoutubeUrl').val();
        const aspectRatio = $('input[name="aspectRatio"]:checked').val();
        
        if (!youtubeUrl) {
            showError("Please enter a YouTube URL");
            return;
        }
        
        showLoading('#createShortsBtn', '#shortsSpinner');
        
        // Call API to create shorts with selected aspect ratio
        $.ajax({
            url: '/api/Transcription/transcribe-extract-create',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ 
                youtubeUrl: youtubeUrl,
                aspectRatio: parseInt(aspectRatio) 
            }),
            success: function(response) {
                hideLoading('#createShortsBtn', '#shortsSpinner');
                if (response.success) {
                    showShortsResult(response.shorts, response.bestMoments, response.transcription);
                } else {
                    showError(response.errorMessage || "Failed to create shorts");
                }
            },
            error: function(xhr) {
                hideLoading('#createShortsBtn', '#shortsSpinner');
                showError(xhr.responseJSON?.errorMessage || "Failed to create shorts. Please try again later.");
            }
        });
    });
    
    // Copy transcription button
    $('#copyTranscriptionBtn').on('click', function() {
        const text = $('#transcriptionResult').text();
        copyToClipboard(text);
        showToast("Transcription copied to clipboard!");
    });
    
    // Download transcription button
    $('#downloadTranscriptionBtn').on('click', function() {
        const text = $('#transcriptionResult').text();
        downloadTextFile(text, "transcription.txt");
    });
    
    // Copy best moments button
    $('#copyBestMomentsBtn').on('click', function() {
        const moments = JSON.parse($('#bestMomentsList').attr('data-moments'));
        copyToClipboard(JSON.stringify(moments, null, 2));
        showToast("Best moments copied to clipboard!");
    });
    
    // Download best moments button
    $('#downloadBestMomentsBtn').on('click', function() {
        const moments = JSON.parse($('#bestMomentsList').attr('data-moments'));
        downloadTextFile(JSON.stringify(moments, null, 2), "best_moments.json");
    });
});

// Helper functions
function showLoading(buttonId, spinnerId) {
    $(buttonId).prop('disabled', true);
    $(spinnerId).removeClass('d-none');
    $('#resultCard').addClass('d-none');
    $('#errorMessage').addClass('d-none');
}

function hideLoading(buttonId, spinnerId) {
    $(buttonId).prop('disabled', false);
    $(spinnerId).addClass('d-none');
}

function showError(message) {
    $('#resultCard').removeClass('d-none');
    $('#errorMessage').removeClass('d-none').text(message);
    $('#transcriptionResultContainer').addClass('d-none');
    $('#bestMomentsResultContainer').addClass('d-none');
    $('#shortsResultContainer').addClass('d-none');
}

function showTranscriptionResult(text) {
    $('#resultCard').removeClass('d-none');
    $('#errorMessage').addClass('d-none');
    $('#transcriptionResultContainer').removeClass('d-none');
    $('#bestMomentsResultContainer').addClass('d-none');
    $('#shortsResultContainer').addClass('d-none');
    
    $('#transcriptionResult').text(text);
}

function showBestMomentsResult(moments, transcription) {
    $('#resultCard').removeClass('d-none');
    $('#errorMessage').addClass('d-none');
    $('#transcriptionResultContainer').addClass('d-none');
    $('#bestMomentsResultContainer').removeClass('d-none');
    $('#shortsResultContainer').addClass('d-none');
    
    $('#bestMomentsList').empty().attr('data-moments', JSON.stringify(moments));
    
    moments.forEach(function(moment, index) {
        const item = `
            <div class="list-group-item">
                <div class="d-flex w-100 justify-content-between">
                    <h5 class="mb-1">Moment ${index + 1}</h5>
                    <small>${moment.startTimestamp} - ${moment.endTimestamp}</small>
                </div>
                <p class="mb-1">${moment.content}</p>
                <small class="text-muted"><i class="fas fa-comment-alt me-1"></i>${moment.reason}</small>
            </div>
        `;
        $('#bestMomentsList').append(item);
    });
    
    if (transcription) {
        $('#transcriptionResult').text(transcription);
    }
}

function showShortsResult(shorts, moments, transcription) {
    $('#resultCard').removeClass('d-none');
    $('#errorMessage').addClass('d-none');
    $('#transcriptionResultContainer').addClass('d-none');
    $('#bestMomentsResultContainer').addClass('d-none');
    $('#shortsResultContainer').removeClass('d-none');
    
    $('#shortsResults').empty();
    
    // Store the source video path for reuse
    window.sourceVideoPath = shorts.length > 0 ? shorts[0].sourceVideoPath : null;
    
    // Show best moments list for preview and selection
    if (moments && moments.length > 0) {
        moments.forEach(function(moment, index) {
            // Find the matching short if it exists
            const matchingShort = shorts.find(s => s.id === index + 1);
            
            const item = `
                <div class="card mb-4 shadow-sm" id="moment-card-${index + 1}">
                    <div class="card-header bg-dark text-white py-2 d-flex justify-content-between align-items-center">
                        <h6 class="mb-0">Moment #${index + 1}: ${moment.startTimestamp} - ${moment.endTimestamp}</h6>
                        <button class="btn btn-sm btn-outline-light preview-moment-btn" data-index="${index + 1}" data-start="${moment.startTimestamp}" data-end="${moment.endTimestamp}">
                            <i class="fas fa-eye me-1"></i>Preview
                        </button>
                    </div>
                    <div class="card-body p-3">
                        <p class="mb-1">${moment.content}</p>
                        <div class="small text-muted mb-3"><i class="fas fa-comment-alt me-1"></i>${moment.reason}</div>
                        
                        <div class="preview-container d-none" id="preview-container-${index + 1}">
                            <div class="ratio ratio-16x9 mb-3">
                                <div id="player-${index + 1}" class="player-container"></div>
                            </div>
                            
                            <div class="mb-3">
                                <label class="form-label">Select frame size for this clip:</label>
                                <div class="d-flex justify-content-between">
                                    <div class="form-check">
                                        <input class="form-check-input aspect-radio" type="radio" name="aspect-${index + 1}" id="landscape-${index + 1}" value="0" checked data-index="${index + 1}">
                                        <label class="form-check-label" for="landscape-${index + 1}">
                                            <i class="fas fa-tv me-1"></i> Landscape
                                        </label>
                                    </div>
                                    <div class="form-check">
                                        <input class="form-check-input aspect-radio" type="radio" name="aspect-${index + 1}" id="portrait-${index + 1}" value="1" data-index="${index + 1}">
                                        <label class="form-check-label" for="portrait-${index + 1}">
                                            <i class="fas fa-mobile-alt me-1"></i> Portrait
                                        </label>
                                    </div>
                                    <div class="form-check">
                                        <input class="form-check-input aspect-radio" type="radio" name="aspect-${index + 1}" id="square-${index + 1}" value="2" data-index="${index + 1}">
                                        <label class="form-check-label" for="square-${index + 1}">
                                            <i class="fas fa-square me-1"></i> Square
                                        </label>
                                    </div>
                                </div>
                            </div>
                            
                            <div class="d-grid gap-2 d-md-flex justify-content-md-end mt-3 action-buttons-${index + 1} d-none">
                                <button class="btn btn-sm btn-success create-clip-btn" data-index="${index + 1}">
                                    <i class="fas fa-cut me-1"></i>Create Clip
                                </button>
                                
                                <a href="#" class="btn btn-sm btn-primary download-clip-btn d-none" id="download-clip-${index + 1}" download>
                                    <i class="fas fa-download me-1"></i>Download Clip
                                </a>
                            </div>
                        </div>
                        
                        <div class="clip-result d-none" id="clip-result-${index + 1}">
                            <div class="ratio ratio-16x9 mb-3">
                                <video id="clip-video-${index + 1}" class="w-100" controls>
                                    <source src="" type="video/mp4">
                                    Your browser does not support the video tag.
                                </video>
                            </div>
                            
                            <div class="d-grid gap-2 d-md-flex justify-content-md-end mt-3">
                                <button class="btn btn-sm btn-outline-success play-clip-btn" data-index="${index + 1}">
                                    <i class="fas fa-play me-1"></i>Play
                                </button>
                                
                                <a href="#" class="btn btn-sm btn-outline-primary download-result-btn" id="download-result-${index + 1}" download>
                                    <i class="fas fa-download me-1"></i>Download
                                </a>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            $('#shortsResults').append(item);
        });
        
        // Add preview functionality
        $('.preview-moment-btn').on('click', function() {
            const index = $(this).data('index');
            const startTime = $(this).data('start');
            const endTime = $(this).data('end');
            
            // Toggle preview container
            $(`#preview-container-${index}`).toggleClass('d-none');
            
            // Initialize or reload player if not already loaded
            if ($(`#player-${index}`).children().length === 0) {
                initYouTubePreview(index, startTime, endTime);
            }
            
            // Show action buttons
            $(`.action-buttons-${index}`).removeClass('d-none');
            
            // Change button text
            if ($(`#preview-container-${index}`).hasClass('d-none')) {
                $(this).html('<i class="fas fa-eye me-1"></i>Preview');
            } else {
                $(this).html('<i class="fas fa-eye-slash me-1"></i>Hide Preview');
            }
        });
        
        // Handle aspect ratio changes
        $('.aspect-radio').on('change', function() {
            const index = $(this).data('index');
            // No immediate action needed - the selection will be used when creating the clip
        });
        
        // Handle create clip button
        $('.create-clip-btn').on('click', function() {
            const index = $(this).data('index');
            const aspectRatio = $(`input[name="aspect-${index}"]:checked`).val();
            const moment = moments[index - 1]; // index is 1-based, array is 0-based
            
            $(this).prop('disabled', true);
            $(this).html('<i class="fas fa-spinner fa-spin me-1"></i>Creating...');
            
            // Use the cached source video path if available
            const requestData = {
                bestMoments: [moment],
                aspectRatio: parseInt(aspectRatio)
            };
            
            // If we have a cached source video path, use it instead of downloading again
            if (window.sourceVideoPath) {
                requestData.sourceVideoPath = window.sourceVideoPath;
                
                $.ajax({
                    url: '/api/shorts/generate-selected',
                    type: 'POST',
                    contentType: 'application/json',
                    data: JSON.stringify({
                        sourceVideoPath: window.sourceVideoPath,
                        selectedShorts: [{
                            id: index,
                            title: `Moment ${index}`,
                            aspectRatio: parseInt(aspectRatio),
                            startTimeSeconds: convertTimestampToSeconds(moment.startTimestamp),
                            endTimeSeconds: convertTimestampToSeconds(moment.endTimestamp),
                            content: moment.content
                        }]
                    }),
                    success: function(response) {
                        if (response.success && response.shorts && response.shorts.length > 0) {
                            const short = response.shorts[0];
                            
                            // Update the clip result container
                            $(`#clip-result-${index}`).removeClass('d-none');
                            
                            // Set correct video source with type
                            const videoElement = document.getElementById(`clip-video-${index}`);
                            videoElement.innerHTML = `<source src="${short.previewUrl}" type="video/mp4">`;
                            videoElement.load(); // Important: reload the video element
                            
                            // Set the download link with a proper filename
                            const aspectName = ['landscape', 'portrait', 'square'][parseInt(aspectRatio)];
                            const downloadFileName = `short_${index}_${aspectName}.mp4`;
                            $(`#download-result-${index}`)
                                .attr('href', short.downloadUrl)
                                .attr('download', downloadFileName);
                            
                            // Re-enable the button
                            $(`.create-clip-btn[data-index="${index}"]`).prop('disabled', false).html('<i class="fas fa-cut me-1"></i>Create Clip');
                            
                            showToast("Clip created successfully!");
                        } else {
                            $(`.create-clip-btn[data-index="${index}"]`).prop('disabled', false).html('<i class="fas fa-cut me-1"></i>Create Clip');
                            showToast("Error creating clip. Please try again.");
                        }
                    },
                    error: function() {
                        $(`.create-clip-btn[data-index="${index}"]`).prop('disabled', false).html('<i class="fas fa-cut me-1"></i>Create Clip');
                        showToast("Error creating clip. Please try again.");
                    }
                });
            } else {
                // Fallback to the original method if no cached video path
                requestData.youtubeUrl = $('#shortsYoutubeUrl').val();
                
                $.ajax({
                    url: '/api/shorts/create-with-aspect',
                    type: 'POST',
                    contentType: 'application/json',
                    data: JSON.stringify(requestData),
                    success: function(response) {
                        if (response.success && response.shorts && response.shorts.length > 0) {
                            const short = response.shorts[0];
                            
                            // Cache the source video path for future use
                            window.sourceVideoPath = response.sourceVideoPath;
                            
                            // Update the clip result container
                            $(`#clip-result-${index}`).removeClass('d-none');
                            
                            // Set correct video source with type
                            const videoElement = document.getElementById(`clip-video-${index}`);
                            videoElement.innerHTML = `<source src="${short.previewUrl}" type="video/mp4">`;
                            videoElement.load(); // Important: reload the video element
                            
                            // Set the download link with a proper filename
                            const aspectName = ['landscape', 'portrait', 'square'][parseInt(aspectRatio)];
                            const downloadFileName = `short_${index}_${aspectName}.mp4`;
                            $(`#download-result-${index}`)
                                .attr('href', short.downloadUrl)
                                .attr('download', downloadFileName);
                            
                            // Re-enable the button
                            $(`.create-clip-btn[data-index="${index}"]`).prop('disabled', false).html('<i class="fas fa-cut me-1"></i>Create Clip');
                            
                            showToast("Clip created successfully!");
                        } else {
                            $(`.create-clip-btn[data-index="${index}"]`).prop('disabled', false).html('<i class="fas fa-cut me-1"></i>Create Clip');
                            showToast("Error creating clip. Please try again.");
                        }
                    },
                    error: function() {
                        $(`.create-clip-btn[data-index="${index}"]`).prop('disabled', false).html('<i class="fas fa-cut me-1"></i>Create Clip');
                        showToast("Error creating clip. Please try again.");
                    }
                });
            }
        });
        
        // Handle play clip button
        $('.play-clip-btn').on('click', function() {
            const index = $(this).data('index');
            const video = document.getElementById(`clip-video-${index}`);
            
            if (video.paused) {
                video.play();
                $(this).html('<i class="fas fa-pause me-1"></i>Pause');
            } else {
                video.pause();
                $(this).html('<i class="fas fa-play me-1"></i>Play');
            }
        });
    }
    
    if (transcription) {
        $('#shortsTranscriptionResult').text(transcription);
    }
}

// Initialize YouTube player for preview
function initYouTubePreview(index, startTime, endTime) {
    const youtubeUrl = $('#shortsYoutubeUrl').val();
    const videoId = getYouTubeVideoId(youtubeUrl);
    
    if (!videoId) {
        showToast("Invalid YouTube URL. Cannot preview.");
        return;
    }
    
    // Convert timestamp format (mm:ss) to seconds
    const startSeconds = convertTimestampToSeconds(startTime);
    
    // Create YouTube player
    new YT.Player(`player-${index}`, {
        videoId: videoId,
        playerVars: {
            start: startSeconds,
            autoplay: 1,
            controls: 1,
            rel: 0
        },
        events: {
            onReady: function(event) {
                // Player is ready
            }
        }
    });
}

// Helper function to extract YouTube video ID from URL
function getYouTubeVideoId(url) {
    const regExp = /^.*((youtu.be\/)|(v\/)|(\/u\/\w\/)|(embed\/)|(watch\?))\??v?=?([^#&?]*).*/;
    const match = url.match(regExp);
    return (match && match[7].length === 11) ? match[7] : false;
}

// Helper function to convert timestamp (mm:ss) to seconds
function convertTimestampToSeconds(timestamp) {
    const parts = timestamp.split(':');
    return parseInt(parts[0]) * 60 + parseInt(parts[1]);
}

function copyToClipboard(text) {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand('copy');
    document.body.removeChild(textarea);
}

function downloadTextFile(text, filename) {
    const element = document.createElement('a');
    element.setAttribute('href', 'data:text/plain;charset=utf-8,' + encodeURIComponent(text));
    element.setAttribute('download', filename);
    element.style.display = 'none';
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
}

function showToast(message) {
    // Simple toast implementation
    const toast = $(`
        <div class="toast-container position-fixed bottom-0 end-0 p-3">
            <div class="toast" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="toast-header">
                    <strong class="me-auto">YShorts</strong>
                    <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        </div>
    `);
    
    $('body').append(toast);
    const bsToast = new bootstrap.Toast(toast.find('.toast'));
    bsToast.show();
    
    setTimeout(function() {
        toast.remove();
    }, 3000);
}
