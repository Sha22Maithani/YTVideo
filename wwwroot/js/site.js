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
    
    shorts.forEach(function(short) {
        const item = `
            <div class="list-group-item">
                <div class="row">
                    <div class="col-md-4">
                        <img src="${short.thumbnailUrl}" class="img-fluid rounded" alt="Thumbnail">
                    </div>
                    <div class="col-md-8">
                        <div class="d-flex w-100 justify-content-between">
                            <h5 class="mb-1">${short.title}</h5>
                            <small>${short.duration}</small>
                        </div>
                        <div class="mt-3">
                            <a href="${short.downloadUrl}" class="btn btn-sm btn-youtube" download>
                                <i class="fas fa-download me-1"></i>Download Short
                            </a>
                        </div>
                    </div>
                </div>
            </div>
        `;
        $('#shortsResults').append(item);
    });
    
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
