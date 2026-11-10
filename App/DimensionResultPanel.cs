using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tolerance.Core;

namespace Tolerance.App;

internal sealed partial class SizePage
{
    private readonly TextBlock[] rangeLabels=new TextBlock[2],rangeNumbers=new TextBlock[2],bandNames=new TextBlock[2];
    private TextBlock? rangeMeaning;
    internal IEnumerable<TextBlock> FitRangeNumbers=>rangeNumbers.Where(t=>t is not null);
    internal IEnumerable<string> FitRangeLabels=>rangeLabels.Select(t=>t.Text);

    private (TextBlock Summary,TextBlock Notice,TextBlock Advice) BuildResultPanel(Grid lower)
    {
        var card=UI.Card();card.Padding=new Thickness(12);lower.Children.Add(card);
        var content=new Grid();card.Child=content;
        foreach(double height in new[]{44d,4d,92d,52d,32d,28d})content.RowDefinitions.Add(new(){Height=new GridLength(height)});

        var hero=new Grid();content.Children.Add(hero);
        hero.ColumnDefinitions.Add(new(){Width=new GridLength(160)});hero.ColumnDefinitions.Add(new());hero.ColumnDefinitions.Add(new());
        var nature=new StackPanel();hero.Children.Add(nature);
        var resultSummary=UI.Text("",16,FontWeights.Medium);nature.Children.Add(resultSummary);
        rangeMeaning=UI.Text("孔尺寸 − 轴尺寸",12);rangeMeaning.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");nature.Children.Add(rangeMeaning);
        for(int i=0;i<2;i++)
        {
            var group=new Grid{Margin=new Thickness(12,0,0,0)};SetColumn(group,i+1);hero.Children.Add(group);
            group.RowDefinitions.Add(new(){Height=new GridLength(16)});group.RowDefinitions.Add(new(){Height=new GridLength(28)});
            var label=UI.Text("",12);label.TextAlignment=TextAlignment.Right;label.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");group.Children.Add(label);rangeLabels[i]=label;
            var number=Number(24);number.SetResourceReference(TextBlock.ForegroundProperty,"Accent");rangeNumbers[i]=number;
            var view=NumericView(number);SetRow(view,1);group.Children.Add(view);
        }

        var table=new Grid();SetRow(table,2);content.Children.Add(table);
        foreach(double width in new[]{80d,112d,112d,112d,168d,170d})table.ColumnDefinitions.Add(new(){Width=new GridLength(width)});
        foreach(double height in new[]{20d,20d,26d,26d})table.RowDefinitions.Add(new(){Height=new GridLength(height)});
        void Caption(string text,int row,int col,int span=1,int rowSpan=1)
        {
            var caption=UI.Text(text,12);caption.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");
            caption.HorizontalAlignment=col==0?HorizontalAlignment.Left:span>1?HorizontalAlignment.Center:HorizontalAlignment.Right;caption.VerticalAlignment=VerticalAlignment.Center;
            if(col>0&&span==1)caption.Margin=new Thickness(0,0,8,0);
            SetRow(caption,row);SetColumn(caption,col);SetColumnSpan(caption,span);SetRowSpan(caption,rowSpan);table.Children.Add(caption);
        }
        Caption(fit?"要素":"单位",0,0,1,2);Caption(fit?"偏差 / mm":"偏差",0,1,2);Caption(fit?"公差 / μm":"公差",0,3,1,2);Caption("极限尺寸 / mm",0,4,2);
        Caption("上偏差",1,1);Caption("下偏差",1,2);Caption("最小尺寸",1,4);Caption("最大尺寸",1,5);
        for(int row=0;row<2;row++)
        {
            string feature=fit?(row==0?"孔":"轴"):(row==0?"mm":"μm"),role=(fit?row==0:singleKind==FeatureKind.Hole)?"FeatureHoleInk":"FeatureShaftInk";
            var background=new Border{BorderThickness=new Thickness(0,1,0,0)};
            background.SetResourceReference(Border.BackgroundProperty,row==0?"ControlSurface":"Surface");background.SetResourceReference(Border.BorderBrushProperty,"Hairline");
            SetRow(background,row+2);SetColumnSpan(background,6);table.Children.Add(background);
            var marker=new Border{Width=2,HorizontalAlignment=HorizontalAlignment.Left};marker.SetResourceReference(Border.BackgroundProperty,role);SetRow(marker,row+2);table.Children.Add(marker);
            var name=UI.Text(feature,14,FontWeights.Medium,new Thickness(8,0,0,0));name.VerticalAlignment=VerticalAlignment.Center;name.SetResourceReference(TextBlock.ForegroundProperty,role);
            SetRow(name,row+2);table.Children.Add(name);bandNames[row]=name;
            for(int col=0;col<5;col++)
            {
                var number=Number(15);fitValues[row,col]=number;var view=NumericView(number);view.Margin=new Thickness(0,0,8,0);SetRow(view,row+2);SetColumn(view,col+1);table.Children.Add(view);
            }
        }
        foreach(int column in new[]{3,4})
        {
            var divider=new Border{Width=1,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,0,0)};
            divider.SetResourceReference(Border.BackgroundProperty,"Hairline");SetColumn(divider,column);SetRowSpan(divider,4);table.Children.Add(divider);
        }

        var metadata=new Grid();metadata.RowDefinitions.Add(new(){Height=new GridLength(16)});metadata.RowDefinitions.Add(new(){Height=new GridLength(36)});SetRow(metadata,3);content.Children.Add(metadata);
        intervalText=UI.Text("",11);intervalText.SetResourceReference(TextBlock.ForegroundProperty,"Muted");metadata.Children.Add(intervalText);
        var gradeAdvice=UI.Text("",12);gradeAdvice.SetResourceReference(TextBlock.ForegroundProperty,"Secondary");SetRow(gradeAdvice,1);metadata.Children.Add(gradeAdvice);

        var actions=new StackPanel{Orientation=Orientation.Horizontal};SetRow(actions,4);content.Children.Add(actions);
        AddCopy(actions,"完整结果",()=>FullText);
        var help=UI.Text(CopyText.DisplayInstructions,11,margin:new Thickness(12,0,0,0));help.VerticalAlignment=VerticalAlignment.Center;actions.Children.Add(help);
        foreach(var button in actions.Children.OfType<Button>()){button.Height=28;button.Padding=new Thickness(8,3,8,3);button.Margin=new Thickness(0,0,6,0);}
        var resultNotice=UI.Text("",11);resultNotice.SetResourceReference(TextBlock.ForegroundProperty,"Muted");SetRow(resultNotice,5);content.Children.Add(resultNotice);
        var plotCard=UI.Card();plotCard.Padding=new Thickness(12);SetColumn(plotCard,2);lower.Children.Add(plotCard);plotCard.Child=Plot;
        return(resultSummary,resultNotice,gradeAdvice);
    }

    private static TextBlock Number(double size)
    {
        var number=UI.Text("—",size);number.FontFamily=new FontFamily("Consolas");number.TextAlignment=TextAlignment.Right;number.TextWrapping=TextWrapping.NoWrap;
        return number;
    }
    private Viewbox NumericView(TextBlock number)=>new(){Child=Target(number).Button,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};

    private void RefreshResultHeading()
    {
        var labels=new[]{"最小值 / mm","最大值 / mm"};decimal[]? values=null;
        if(Fit is {} f)
        {
            summary.Text=f.Nature;
            if(f.MinimumDifferenceUm>=0){labels=["最小间隙 / mm","最大间隙 / mm"];values=[f.MinimumDifferenceUm/1000m,f.MaximumDifferenceUm/1000m];rangeMeaning!.Text="孔尺寸 − 轴尺寸";}
            else if(f.MaximumDifferenceUm<=0){labels=["最小过盈 / mm","最大过盈 / mm"];values=[-f.MaximumDifferenceUm/1000m,-f.MinimumDifferenceUm/1000m];rangeMeaning!.Text="轴尺寸 − 孔尺寸";}
            else{labels=["最大间隙 / mm","最大过盈 / mm"];values=[f.MaximumDifferenceUm/1000m,-f.MinimumDifferenceUm/1000m];rangeMeaning!.Text="间隙与过盈分别显示";}
        }
        else if(!fit&&(Hole??Shaft) is {} single)
        {
            summary.Text=$"{(singleKind==FeatureKind.Hole?"孔":"轴")} {single.Band}";
            labels=["上偏差 / mm","下偏差 / mm"];values=[single.UpperUm/1000m,single.LowerUm/1000m];
            rangeMeaning!.Text="公差 = 上偏差 − 下偏差";
        }
        else{summary.Text="查询条件无效";summary.ToolTip=Error;rangeMeaning!.Text="请调整尺寸或公差带";}
        var shown=CopyValue.Align(values is null?["—","—"]:values.Select(value=>fit?F(value):S(value)).ToArray());
        for(int i=0;i<2;i++){rangeLabels[i].Text=labels[i];Target(rangeNumbers[i]).Update(new(shown[i],labels[i].Replace(" / mm",""),"mm",HasResult));}
        for(int row=0;row<2;row++)
        {
            var result=fit?(row==0?Hole:Shaft):Hole??Shaft;
            bandNames[row].Text=fit?$"{(row==0?"孔":"轴")}  {result?.Band??"—"}":row==0?"mm":"μm";
            bandNames[row].ToolTip=result is null?null:$"{(result.Kind==FeatureKind.Hole?"孔":"轴")} {result.Band} · {(fit||row==0?"mm":"μm")}\n命中尺寸段：{result.Interval}";
            for(int col=0;col<5;col++)fitValues[row,col].ToolTip=fitValues[row,col].Text;
        }
        summary.ToolTip=HasResult?Fit?.Range(F)??"公差宽度 = 上偏差 − 下偏差":Error;
    }
}
