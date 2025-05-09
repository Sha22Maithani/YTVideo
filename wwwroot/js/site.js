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
        
        if (!youtubeUrl) {
            showError("Please enter a YouTube URL");
            return;
        }
        
        showLoading('#createShortsBtn', '#shortsSpinner');
        
        // Call API to create shorts
        $.ajax({
            url: '/api/Transcription/transcribe-extract-create',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ youtubeUrl: youtubeUrl }),
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
    
    // Process all shorts and add transcriptions from corresponding moments
    shorts.forEach(function(short, index) {
        // Match the short with its corresponding moment based on title or index
        // The title may contain the start timestamp which we can use to match
        let matchedMoment = null;
        
        if (moments && moments.length > 0) {
            // Try to find a moment that matches this short
            // First attempt: by index if they're in the same order
            if (index < moments.length) {
                matchedMoment = moments[index];
            }
            
            // Second attempt: try to match by timestamp in the title
            if (!matchedMoment) {
                const titleTimestamp = short.title.match(/(\d+:\d+)/);
                if (titleTimestamp) {
                    matchedMoment = moments.find(m => m.startTimestamp === titleTimestamp[1]);
                }
            }
        }
        
        // Get the content for this short
        const shortContent = matchedMoment ? matchedMoment.content : "No transcription available for this segment.";
        
        const item = `
            <div class="card mb-4 shadow-sm">
                <div class="card-header bg-dark text-white py-2">
                    <h6 class="mb-0">Short #${short.id}: ${short.title}</h6>
                </div>
                <div class="card-body p-0">
                    <!-- Video player - always visible -->
                    <div class="ratio ratio-16x9">
                        <video id="video-${short.id}" class="w-100" controls poster="${short.thumbnailUrl}">
                            <source src="${short.previewUrl}" type="video/mp4">
                            Your browser does not support the video tag.
                        </video>
                    </div>
                    
                    <!-- Transcription section for this short -->
                    <div class="p-3">
                        <div class="d-flex justify-content-between align-items-center mb-2">
                            <div><small class="text-muted">Duration: ${short.duration}</small></div>
                            <div>
                                <button class="btn btn-sm btn-outline-success me-2 play-video-btn" data-id="${short.id}">
                                    <i class="fas fa-play me-1"></i>Play
                                </button>
                                <a href="${short.downloadUrl}" class="btn btn-sm btn-outline-primary" download>
                                    <i class="fas fa-download me-1"></i>Download
                                </a>
                            </div>
                        </div>
                        
                        <div class="mt-3">
                            <h6 class="border-bottom pb-2">Transcription</h6>
                            <p class="small text-muted mb-0">${shortContent}</p>
                            ${matchedMoment ? `<div class="mt-2 small"><strong>Why it's great:</strong> ${matchedMoment.reason}</div>` : ''}
                        </div>
                    </div>
                </div>
            </div>
        `;
        $('#shortsResults').append(item);
    });
    
    // Add event listener for play buttons
    $('.play-video-btn').on('click', function(e) {
        e.preventDefault();
        const videoId = $(this).data('id');
        const video = document.getElementById(`video-${videoId}`);
        
        // Pause any other playing videos first
        $('video').each(function() {
            if (this.id !== `video-${videoId}` && !this.paused) {
                this.pause();
            }
        });
        
        // Play/pause this video
        if (video.paused) {
            video.play().catch(err => console.log('Error playing video:', err));
            $(this).html('<i class="fas fa-pause me-1"></i>Pause');
        } else {
            video.pause();
            $(this).html('<i class="fas fa-play me-1"></i>Play');
        }
    });
    
    // Listen for video play/pause events to update buttons
    $('video').on('play', function() {
        const videoId = this.id.replace('video-', '');
        $(`button[data-id="${videoId}"].play-video-btn`).html('<i class="fas fa-pause me-1"></i>Pause');
    });
    
    $('video').on('pause', function() {
        const videoId = this.id.replace('video-', '');
        $(`button[data-id="${videoId}"].play-video-btn`).html('<i class="fas fa-play me-1"></i>Play');
    });
    
    // Also show the full list of best moments and transcription in the details section
    if (moments) {
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
    }
    
    if (transcription) {
        $('#shortsTranscriptionResult').text(transcription);
    }
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
