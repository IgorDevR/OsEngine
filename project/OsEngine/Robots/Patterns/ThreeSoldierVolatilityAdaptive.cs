﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Patterns
{
    [Bot("ThreeSoldierVolatilityAdaptive")]
    public class ThreeSoldierVolatilityAdaptive : BotPanel
    {
        public ThreeSoldierVolatilityAdaptive(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });

            Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            Slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            HeightSoldiers = CreateParameter("Height soldiers %", 1, 0, 20, 1m);

            MinHeightOneSoldier = CreateParameter("Min height one soldier %", 0.2m, 0, 20, 1m);

            ProcHeightTake = CreateParameter("Profit % from height of pattern", 50m, 0, 20, 1m);

            ProcHeightStop = CreateParameter("Stop % from height of pattern", 20m, 0, 20, 1m);

            DaysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 1, 0, 20, 1);

            HeightSoldiersVolaPecrent = CreateParameter("Height soldiers volatility percent", 5, 0, 20, 1m);

            MinHeightOneSoldiersVolaPecrent = CreateParameter("Min height one soldier volatility percent", 1, 0, 20, 1m);

            Description = "Trading robot Three Soldiers adaptive by volatility. " +
                "When forming a pattern of three growing / falling candles, " +
                "the entrance to the countertrend with a fixation on a profit or a stop";
        }

        public override string GetNameStrategyType()
        {
            return "ThreeSoldierVolatilityAdaptive";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabSimple _tab;

        // settings

        public StrategyParameterString Regime;
        public StrategyParameterDecimal HeightSoldiers;
        public StrategyParameterDecimal MinHeightOneSoldier;
        public StrategyParameterDecimal ProcHeightTake;
        public StrategyParameterDecimal ProcHeightStop;
        public StrategyParameterDecimal Slippage;
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterInt DaysVolatilityAdaptive;
        public StrategyParameterDecimal HeightSoldiersVolaPecrent;
        public StrategyParameterDecimal MinHeightOneSoldiersVolaPecrent;

        // volatility adaptation

        private void AdaptSoldiersHeight(List<Candle> candles)
        {

            if (DaysVolatilityAdaptive.ValueInt <= 0
                || HeightSoldiersVolaPecrent.ValueDecimal <= 0
                || MinHeightOneSoldiersVolaPecrent.ValueDecimal <= 0)
            {
                return;
            }

            // 1 рассчитываем движение от хая до лоя внутри N дней

            decimal minValueInDay = decimal.MaxValue;
            decimal maxValueInDay = decimal.MinValue;

            List<decimal> volaInDaysPercent = new List<decimal>();

            DateTime date = candles[candles.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                Candle curCandle = candles[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysPercent.Add(volaPercentToday);


                    minValueInDay = decimal.MaxValue;
                    maxValueInDay = decimal.MinValue;
                }

                if (days >= DaysVolatilityAdaptive.ValueInt)
                {
                    break;
                }

                if (curCandle.High > maxValueInDay)
                {
                    maxValueInDay = curCandle.High;
                }
                if (curCandle.Low < minValueInDay)
                {
                    minValueInDay = curCandle.Low;
                }

                if (i == 0)
                {
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);
                    volaInDaysPercent.Add(volaPercentToday);
                }
            }

            if (volaInDaysPercent.Count == 0)
            {
                return;
            }

            // 2 усредняем это движение. Нужна усреднённая волатильность. процент

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 считаем размер свечей с учётом этой волатильности

            decimal allSoldiersHeight = volaPercentSma * (HeightSoldiersVolaPecrent.ValueDecimal / 100);
            decimal oneSoldiersHeight = volaPercentSma * (MinHeightOneSoldiersVolaPecrent.ValueDecimal / 100);

            HeightSoldiers.ValueDecimal = allSoldiersHeight;
            MinHeightOneSoldier.ValueDecimal = oneSoldiersHeight;
        }

        // logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            if(candles.Count > 20 &&
                candles[candles.Count-1].TimeStart.Date != candles[candles.Count-2].TimeStart.Date) 
            {
                AdaptSoldiersHeight(candles);
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles);
            }
            else
            {
                LogicClosePosition(candles);
            }
        }

        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < HeightSoldiers.ValueDecimal)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 3].Close)
                / (candles[candles.Count - 3].Close / 100) < MinHeightOneSoldier.ValueDecimal)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 2].Open - candles[candles.Count - 2].Close)
                / (candles[candles.Count - 2].Close / 100) < MinHeightOneSoldier.ValueDecimal)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < MinHeightOneSoldier.ValueDecimal)
            {
                return;
            }

            //  long
            if (Regime.ValueString != "OnlyShort")
            {
                if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _lastPrice * (Slippage.ValueDecimal / 100));
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
                {
                    _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _lastPrice * (Slippage.ValueDecimal / 100));
                }
            }

            return;

        }

        private void LogicClosePosition(List<Candle> candles)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].StopOrderPrice != 0)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern =
                        Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 4].Open - _tab.CandlesAll[_tab.CandlesAll.Count - 2].Close);

                    decimal priceStop = _lastPrice - (heightPattern * ProcHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice + (heightPattern * ProcHeightTake.ValueDecimal) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop - priceStop * (Slippage.ValueDecimal / 100));
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake - priceStop * (Slippage.ValueDecimal / 100));
                }
                else
                {
                    decimal heightPattern = Math.Abs(_tab.CandlesAll[_tab.CandlesAll.Count - 2].Close - _tab.CandlesAll[_tab.CandlesAll.Count - 4].Open);
                    decimal priceStop = _lastPrice + (heightPattern * ProcHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice - (heightPattern * ProcHeightTake.ValueDecimal) / 100;
                    _tab.CloseAtStop(openPositions[i], priceStop, priceStop + priceStop * (Slippage.ValueDecimal / 100));
                    _tab.CloseAtProfit(openPositions[i], priceTake, priceTake + priceStop * (Slippage.ValueDecimal / 100));
                }
            }
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }
}