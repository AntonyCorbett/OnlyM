﻿using System;
using System.Threading.Tasks;
using System.Windows;
using OnlyM.Core.Models;

namespace OnlyM.MediaElementAdaption;

internal interface IMediaElement
{
    protected static TimeSpan PositionChangedInterval = TimeSpan.FromMilliseconds(160);

    event EventHandler<OnlyMMediaOpenedEventArgs> MediaOpened;

    event EventHandler<OnlyMMediaClosedEventArgs> MediaClosed;

    event EventHandler<OnlyMMediaEndedEventArgs> MediaEnded;

    event EventHandler<OnlyMMediaFailedEventArgs> MediaFailed;

    event EventHandler<OnlyMRenderSubtitlesEventArgs> RenderingSubtitles;

    event EventHandler<OnlyMPositionChangedEventArgs> PositionChanged;

    event EventHandler<OnlyMLogMessageEventArgs> MessageLogged;

    TimeSpan Position { get; set; }

    Duration NaturalDuration { get; }

    bool IsPaused { get; }

    Guid MediaItemId { get; set; }

    FrameworkElement FrameworkElement { get; }

    Task Play(Uri mediaPath, MediaClassification mediaClassification);

    Task Pause();

    Task Close();

    void UnsubscribeEvents();
}
